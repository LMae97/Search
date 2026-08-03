using WeByte.Search.Application.Export;

namespace WeByte.Search.Tests;

/// <summary>Aritmetica pura: nessuna ricerca, nessuno store.</summary>
public class ExportSizeLimitTests
{
    [Fact]
    public void Max_sync_rows_is_the_cell_budget_divided_by_the_columns()
    {
        var limit = new ExportSizeLimit { MaxSyncCells = 200_000 };

        Assert.Equal(20_000, limit.MaxSyncRows(10));
        Assert.Equal(5_000, limit.MaxSyncRows(40));
    }

    [Fact]
    public void Zero_columns_does_not_divide_by_zero()
    {
        var limit = new ExportSizeLimit { MaxSyncCells = 200_000 };

        Assert.Equal(200_000, limit.MaxSyncRows(0));
    }

    [Fact]
    public void Exceeds_sync_compares_the_row_count_against_the_computed_threshold()
    {
        var limit = new ExportSizeLimit { MaxSyncCells = 100 }; // 10 colonne ⇒ soglia 10 righe

        Assert.False(limit.ExceedsSync(10, 10));  // esattamente al limite: non sfonda
        Assert.True(limit.ExceedsSync(11, 10));
    }
}
