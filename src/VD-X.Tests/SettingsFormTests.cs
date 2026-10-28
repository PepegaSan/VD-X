using System.Runtime.ExceptionServices;

namespace VDX.Tests;

public class SettingsFormTests
{
    [Fact]
    public void OffEntryInTheDesktopColumnShowsItsTextAndSavesAsZero()
    {
        RunOnStaThread(() =>
        {
            using var form = new Form();
            var grid = new DataGridView();
            grid.Columns.Add(SettingsForm.DesktopColumn(12, offText: "Off"));
            grid.Columns.Add(new DataGridViewTextBoxColumn());
            form.Controls.Add(grid);

            grid.Rows.Add(0, "game.exe");      // switched off
            grid.Rows.Add(3, "resolve.exe");   // desktop 3

            Assert.Equal("Off", grid.Rows[0].Cells[0].FormattedValue);
            Assert.Equal("3", grid.Rows[1].Cells[0].FormattedValue);
            Assert.Equal(0, grid.Rows[0].Cells[0].Value);   // what ReadPrograms stores as Desktop
            Assert.Equal(3, grid.Rows[1].Cells[0].Value);
        });
    }

    [Fact]
    public void RuleColumnsHaveNoOffEntry()
    {
        RunOnStaThread(() =>
        {
            var column = SettingsForm.DesktopColumn(14);
            object first = ((System.Collections.IList)column.DataSource!)[0]!;
            Assert.Equal(1, first.GetType().GetProperty("Value")!.GetValue(first));
        });
    }

    // WinForms controls want a single-threaded apartment; xunit runs tests on MTA threads.
    static void RunOnStaThread(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}