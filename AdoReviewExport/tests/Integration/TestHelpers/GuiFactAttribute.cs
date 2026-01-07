using System;
using Xunit;

namespace Integration.TestHelpers;

/// <summary>
/// GUI 統合テストをオプトインにするための Fact 属性。
/// 既定では WinUI 起動が環境依存で不安定なためスキップし、
/// ADO_REVIEW_EXPORT_RUN_GUI_TESTS=1 のときのみ実行する。
/// </summary>
public sealed class GuiFactAttribute : FactAttribute
{
    public GuiFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_RUN_GUI_TESTS"),
                "1",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "GUI 統合テストは既定で無効です。実行する場合は ADO_REVIEW_EXPORT_RUN_GUI_TESTS=1 を設定してください。";
        }
    }
}
