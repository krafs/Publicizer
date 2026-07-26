using dnlib.DotNet;
using dnlib.DotNet.MD;

namespace Publicizer;

/// <summary>
/// Writes a publicized assembly by patching visibility bits directly in a copy of the original file.
/// </summary>
/// <remarks>
/// Publicization only ever flips bits in the Flags column of the TypeDef, Field and Method tables
/// (see <see cref="AssemblyEditor"/>) — nothing is added, removed, renamed or resized. Those columns are
/// fixed-width at fixed offsets, so the result can be produced by patching bytes instead of having dnlib
/// tear down and rebuild the entire metadata, which is two to three orders of magnitude more expensive.
///
/// Patching also leaves every other byte identical to the input, so output is stable across dnlib upgrades
/// and unaffected by the writer quirk that KeepOldMaxStack works around.
///
/// Layouts this cannot handle are rejected by <see cref="TryWrite"/> so the caller can fall back to the
/// dnlib writer.
/// </remarks>
internal static class InPlaceWriter
{
    // ECMA-335 II.22.37/15/26: index of the Flags column within each table's row.
    private const int TypeDefFlagsColumnIndex = 0;
    private const int FieldFlagsColumnIndex = 0;
    private const int MethodFlagsColumnIndex = 2;

    private const string FlagsColumnName = "Flags";

    /// <summary>
    /// Attempts to write <paramref name="module"/>'s publicized form to <paramref name="destinationPath"/> by
    /// patching a copy of <paramref name="sourcePath"/>. Returns false when the assembly's metadata layout is
    /// not patchable, in which case nothing has been written and the caller should use the dnlib writer.
    /// </summary>
    internal static bool TryWrite(ModuleDefMD module, string sourcePath, string destinationPath, ITaskLogger logger)
    {
        Metadata metadata = module.Metadata;

        // ENC/uncompressed metadata (#- heap) allows deleted rows and non-sequential rids, so row offsets
        // cannot be computed from rid alone.
        if (!metadata.IsCompressed)
        {
            logger.Info("Metadata is not compressed (#- heap); falling back to the dnlib writer");
            return false;
        }

        TablesStream tables = metadata.TablesStream;
        byte[] buffer = File.ReadAllBytes(sourcePath);

        if (!TryPatchTable(buffer, tables.TypeDefTable, TypeDefFlagsColumnIndex, module.ResolveTypeDefFlags, logger) ||
            !TryPatchTable(buffer, tables.FieldTable, FieldFlagsColumnIndex, module.ResolveFieldFlags, logger) ||
            !TryPatchTable(buffer, tables.MethodTable, MethodFlagsColumnIndex, module.ResolveMethodFlags, logger))
        {
            return false;
        }

        File.WriteAllBytes(destinationPath, buffer);
        return true;
    }

    private static bool TryPatchTable(byte[] buffer, MDTable table, int columnIndex, Func<uint, uint?> getFlags, ITaskLogger logger)
    {
        if (table is null || table.Rows == 0)
        {
            return true;
        }

        if (columnIndex >= table.Columns.Count)
        {
            logger.Info($"Table {table.Name} has no column {columnIndex}; falling back to the dnlib writer");
            return false;
        }

        ColumnInfo column = table.Columns[columnIndex];

        // Guards against dnlib ever reordering or resizing the column out from under these constants.
        if (!string.Equals(column.Name, FlagsColumnName, StringComparison.Ordinal))
        {
            logger.Info($"Table {table.Name} column {columnIndex} is '{column.Name}', not '{FlagsColumnName}'; falling back to the dnlib writer");
            return false;
        }

        if (column.Size is not (2 or 4))
        {
            logger.Info($"Table {table.Name} flags column is {column.Size} bytes; falling back to the dnlib writer");
            return false;
        }

        long tableStart = (long)table.StartOffset;
        long rowSize = table.RowSize;
        long lastByte = tableStart + ((table.Rows - 1) * rowSize) + column.Offset + column.Size;

        if (tableStart < 0 || lastByte > buffer.Length)
        {
            logger.Info($"Table {table.Name} extends past the end of the file; falling back to the dnlib writer");
            return false;
        }

        for (uint rid = 1; rid <= table.Rows; rid++)
        {
            uint? flags = getFlags(rid);
            if (flags is null)
            {
                logger.Info($"Table {table.Name} row {rid} did not resolve; falling back to the dnlib writer");
                return false;
            }

            long offset = tableStart + ((rid - 1) * rowSize) + column.Offset;
            uint value = flags.Value;

            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            if (column.Size == 4)
            {
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 3] = (byte)(value >> 24);
            }
        }

        return true;
    }

    private static uint? ResolveTypeDefFlags(this ModuleDefMD module, uint rid) => module.ResolveTypeDef(rid) is TypeDef type ? (uint)type.Attributes : null;
    private static uint? ResolveFieldFlags(this ModuleDefMD module, uint rid) => module.ResolveField(rid) is FieldDef field ? (uint)field.Attributes : null;
    private static uint? ResolveMethodFlags(this ModuleDefMD module, uint rid) => module.ResolveMethod(rid) is MethodDef method ? (uint)method.Attributes : null;
}
