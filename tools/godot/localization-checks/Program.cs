using System.Globalization;
using System.Text.Json;
using Empire.Localization;

var assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition)
        throw new Exception(message);
}
void Reject(Action action, string diagnostic)
{
    assertions++;
    try
    {
        action();
    }
    catch (LocalizationException error) when (error.Message.Contains(diagnostic, StringComparison.Ordinal)) { return; }
    throw new Exception("Expected localization rejection: " + diagnostic);
}
GameText Catalog(Dictionary<string, string> messages, CultureInfo? culture = null) =>
    new(key => messages.GetValueOrDefault(key), culture ?? CultureInfo.GetCultureInfo("zh-CN"));

var first = Catalog(new()
{
    ["test.order"] = "{1} 与 {0}、{1}",
    ["test.literal"] = "{{旗}} {0}",
    ["test.number"] = "数值 {0:F2}",
    ["test.zero"] = "准备完毕",
});
Check(first.Format("test.order", "甲", "乙") == "乙 与 甲、乙", "Reordered/repeated arguments were not respected.");
Check(first.Format("test.literal", "军") == "{旗} 军", "Escaped braces were not preserved.");
Check(first.Format("test.zero") == "准备完毕", "Zero-argument message failed.");
Reject(() => first.Format("test.missing"), "Missing localization key");
Reject(() => first.ValidateKeys(["test.zero", "test.missing"]), "Missing localization key");
Reject(() => first.Format("test.order", "甲"), "requires 2 arguments");
Reject(() => first.Format("test.order", "甲", "乙", "丙"), "requires 2 arguments");
Reject(() => first.Format("test.zero", 1), "requires 0 arguments");
Reject(() => Catalog(new() { ["test.bad"] = " " }).Format("test.bad"), "empty value");
Reject(() => Catalog(new() { ["test.bad"] = "{0" }).Format("test.bad", 1), "malformed composite");
Reject(() => Catalog(new() { ["test.bad"] = "{1}" }).Format("test.bad", 1, 2), "gap");
Reject(() => Catalog(new() { ["test.bad"] = "{0:Q}" }).Format("test.bad", 1), "incompatible argument");

var second = Catalog(new() { ["test.order"] = "{0} 和 {1}" });
Check(second.Format("test.order", "甲", "乙") == "甲 和 乙", "Second catalog was not independently injected.");
Check(first.Format("test.order", "甲", "乙") == "乙 与 甲、乙", "Second catalog changed the first catalog.");
var previousCulture = CultureInfo.CurrentCulture;
try
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
    Check(first.Format("test.number", 1234.5) == "数值 1234.50", "Ambient culture changed formatting.");
}
finally { CultureInfo.CurrentCulture = previousCulture; }
var suppliedCulture = (CultureInfo)CultureInfo.GetCultureInfo("zh-CN").Clone();
var ownedCulture = Catalog(new() { ["test.number"] = "{0:F1}" }, suppliedCulture);
suppliedCulture.NumberFormat.NumberDecimalSeparator = "!";
Check(ownedCulture.Format("test.number", 1.5) == "1.5", "Caller mutated formatter culture.");
var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[0]))!;
var real = Catalog(catalog);
real.ValidateKeys(catalog.Keys);
Check(real.Format("feedback.save.round_saved", 3) == "已保存第 3 回合的进度", "Real parameterized Chinese message failed.");
Check(real.Format("error.load.failed_unchanged") == "读取失败，当前进度未变。", "Real error message failed.");
Console.WriteLine($"PASS localization formatter: {assertions} assertions; all {catalog.Count} real catalog formats validated.");
