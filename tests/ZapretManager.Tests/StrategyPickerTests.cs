using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ZapretManager.App.Core;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class StrategyPickerTests
{
    [Fact]
    public void SetItems_SelectsRequestedStrategyWithoutNativeComboBoxState()
    {
        using var picker = new StrategyPicker();
        var strategies = new[]
        {
            new StrategyInfo("general.bat", "C:/runtime/general.bat", false),
            new StrategyInfo("general (ALT).bat", "C:/runtime/general (ALT).bat", true)
        };

        picker.SetItems(strategies, strategies[1]);

        Assert.Equal(2, picker.Items.Count);
        Assert.Same(strategies[1], picker.SelectedItem);
        Assert.Equal(AccessibleRole.ComboBox, picker.AccessibleRole);
        Assert.Contains("general (ALT)", picker.AccessibleDescription);
    }

    [Fact]
    public void SetItems_WithoutPersistedSelection_DoesNotDisplayFirstStrategyAsSelected()
    {
        using var picker = new StrategyPicker();
        var strategies = new[]
        {
            new StrategyInfo("general.bat", "C:/runtime/general.bat", false),
            new StrategyInfo("general2.bat", "C:/runtime/general2.bat", false)
        };

        picker.SetItems(strategies, selectedItem: null);

        Assert.Null(picker.SelectedItem);
        Assert.Contains("не выбрана", picker.AccessibleDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CalculateBounds_ClampsPopupAtRightEdge()
    {
        var allowed = new Rectangle(100, 100, 632, 500);
        var field = new Rectangle(600, 180, 360, 34);

        var popup = StrategyPickerDropDown.CalculateBounds(field, allowed, preferredWidth: 360, itemCount: 4);

        Assert.Equal(allowed.Right, popup.Right);
        Assert.True(popup.Left >= allowed.Left);
        Assert.True(popup.Bottom <= allowed.Bottom);
    }

    [Fact]
    public void CalculateBounds_OpensAboveFieldNearBottomEdge()
    {
        var allowed = new Rectangle(100, 100, 632, 500);
        var field = new Rectangle(120, 560, 360, 34);

        var popup = StrategyPickerDropDown.CalculateBounds(field, allowed, preferredWidth: 360, itemCount: 7);

        Assert.True(popup.Bottom < field.Top);
        Assert.True(popup.Top >= allowed.Top);
        Assert.True(popup.Bottom <= allowed.Bottom);
    }

    [Fact]
    public void CalculateBounds_LimitsTallStrategyListToSevenRows()
    {
        var allowed = new Rectangle(100, 100, 632, 700);
        var field = new Rectangle(120, 180, 360, 34);

        var sevenRows = StrategyPickerDropDown.CalculateBounds(field, allowed, preferredWidth: 360, itemCount: 7);
        var popup = StrategyPickerDropDown.CalculateBounds(field, allowed, preferredWidth: 360, itemCount: 100);

        Assert.Equal(sevenRows.Height, popup.Height);
        Assert.True(popup.Bottom <= allowed.Bottom);
    }

    [Fact]
    public void StrategyResultsList_LimitsResultsToThreeRows()
    {
        using var list = new StrategyResultsList();

        list.SetItems(["one.bat", "two.bat", "three.bat", "four.bat"]);

        Assert.Equal(["one.bat", "two.bat", "three.bat"], list.Items);
        Assert.DoesNotContain("four.bat", list.AccessibleDescription);
    }

    [Fact]
    public void PickerList_KeyboardHighlightCommitsWithoutSystemSelectionState()
    {
        var committedIndex = -1;
        var strategies = new[]
        {
            new StrategyInfo("general.bat", "C:/runtime/general.bat", true),
            new StrategyInfo("general2.bat", "C:/runtime/general2.bat", false)
        };
        using var list = new StrategyPickerList(
            strategies,
            selectedIndex: 0,
            onCommit: index => committedIndex = index,
            onKey: _ => { });

        list.SetVisibleItemCount(2);
        list.MoveHighlight(1);
        list.CommitHighlight();

        Assert.Equal(1, committedIndex);
        Assert.Equal(AccessibleRole.List, list.AccessibleRole);
        Assert.Contains("2 из 2", list.AccessibleDescription);
    }

    [Fact]
    public void MouseSelection_CanRefreshItemsWithoutDisposingPopupInsideItsOwnEvent()
    {
        Exception? failure = null;
        var selectedFileName = string.Empty;
        var thread = new Thread(() =>
        {
            try
            {
                var strategies = new[]
                {
                    new StrategyInfo("general.bat", "C:/runtime/general.bat", true),
                    new StrategyInfo("general2.bat", "C:/runtime/general2.bat", false)
                };
                using var form = new Form { ClientSize = new Size(420, 180) };
                using var picker = new StrategyPicker
                {
                    Location = new Point(20, 20),
                    Size = new Size(360, 34)
                };
                picker.SetItems(strategies, strategies[0]);
                picker.SelectedItemChanged += (_, _) =>
                {
                    selectedFileName = picker.SelectedItem?.FileName ?? string.Empty;
                    picker.SetItems(strategies, picker.SelectedItem);
                };
                form.Controls.Add(picker);
                form.Show();
                picker.OpenDropDown();
                Application.DoEvents();

                var dropDownField = typeof(StrategyPicker).GetField(
                    "_dropDown",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                var dropDown = Assert.IsType<StrategyPickerDropDown>(dropDownField.GetValue(picker));
                var listField = typeof(StrategyPickerDropDown).GetField(
                    "_list",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                var list = Assert.IsType<StrategyPickerList>(listField.GetValue(dropDown));
                var mouseDown = typeof(StrategyPickerList).GetMethod(
                    "OnMouseDown",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;

                mouseDown.Invoke(
                    list,
                    [new MouseEventArgs(MouseButtons.Left, clicks: 1, x: 20, y: 40, delta: 0)]);
                Application.DoEvents();
                Application.DoEvents();

                Assert.False(picker.IsDropDownOpen);
                form.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA UI regression test timed out.");

        Assert.Null(failure);
        Assert.Equal("general2.bat", selectedFileName);
    }
}
