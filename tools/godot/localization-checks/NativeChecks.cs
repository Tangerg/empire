using System.Text.Json;
using Empire.Localization;
using Godot;

public partial class NativeChecks : Node
{
    public override void _Ready()
    {
        try
        {
            TranslationServer.SetLocale(GodotTextCatalog.Locale);
            var locales = TranslationServer.GetLoadedLocales();
            Require(locales.Length == 1 && locales[0] == GodotTextCatalog.Locale, "Only zh_CN may be registered.");
            var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString("res://expected.json"))!;
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            text.ValidateKeys(expected.Keys);
            foreach (var pair in expected)
                Require(TranslationServer.Translate(pair.Key).ToString() == pair.Value, $"Native catalog message differs: {pair.Key}.");
            Require(text.Format("feedback.save.round_saved", 12) == "已保存第 12 回合的进度", "Native composite formatting differs.");
            try
            {
                text.Format("test.deliberately_missing");
                throw new InvalidOperationException("Missing key was silently accepted.");
            }
            catch (LocalizationException) { }
            GD.Print($"PASS native localization resource: {expected.Count} exact messages; zh_CN only; strict key and formatting behavior.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
