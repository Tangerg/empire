# Native presentation text

This directory owns localization infrastructure and the initial Simplified Chinese wording. Native narrative, title-menu and staging controls consume it, as do structured command/session refusal messages. Built-in level/unit names, remaining battle feedback and outcome/progress wording are separate unfinished consumers; this is not a claim that the whole game is localized. Domain refusals, outcomes and progress must remain semantic facts. Never put localized text or localization keys in battle state, commands, events, saves or digests.

## Consumer API

The application composition root creates one text service and explicitly passes it to the presentation/projection objects that need it:

```csharp
TranslationServer.SetLocale(GodotTextCatalog.Locale);
var text = GodotTextCatalog.LoadSimplifiedChinese();
var saved = text.Format("feedback.save.round_saved", round);
var cancel = text.Format("ui.common.cancel");
```

The application registers the resource in `project.godot`:

```ini
[internationalization]
locale/translations=PackedStringArray("res://Localization/zh_CN.po")
```

`LoadSimplifiedChinese()` uses Godot's native `Translation` resource loader. It validates the resource locale and all imported message formats. It does not change global locale, add an autoload, install a second catalog, or select another language. Locale selection belongs to the composition root. Direct use of the loaded resource prevents an ambient locale change from altering an already-created formatter.

Pure C# projections/tests use the same class without referencing Godot:

```csharp
var text = new GameText(key => catalog.GetValueOrDefault(key), CultureInfo.GetCultureInfo("zh-CN"));
text.ValidateKeys(requiredKeys);
```

The lookup must be immutable for the lifetime of the service. `GameText` copies the culture settings and caches parsed formats. It owns formatting and validation only, not another mutable wording source. Link `GameText.cs` in existing pure test projects; do not link `GodotTextCatalog.cs` or add a Godot dependency to Core.

## Message contract

- `Format(string key, params object?[] arguments)` translates a complete message and then applies standard .NET composite formatting. Numbers remain numbers until presentation.
- `ValidateKeys(IEnumerable<string> requiredKeys)` checks explicit content/UI key coverage before rendering. Required-key inventories belong to the consuming content or module, not a second manually maintained copy of the catalog.
- Keys are semantic, stable dotted IDs. The source guard requires lowercase ASCII segments and permits digits, hyphens and underscores. Do not use the original Chinese wording, array position, or a hash as identity.
- Placeholders use `{0}`, `{1}`, etc. Reordering and repetition are supported. Indices must be contiguous from zero, and the call must supply exactly that many arguments. Escaped braces use `{{` and `}}`. Translator comments name each argument's meaning and type.
- Missing keys, empty values, invalid formats, index gaps, missing/extra arguments and incompatible formats throw `LocalizationException`. There is no raw-key, embedded-Chinese, empty-string, or alternate-language fallback.
- Localize full sentences. Do not concatenate translated fragments to recreate the old grammar. When semantics differ, give the complete messages separate keys.
- Resolve built-in content names from stable IDs at presentation boundaries. Actual user-authored names/free text bypass translation. Disable Control auto-translation where literal user text is displayed.
- Exceptions are diagnostics, not player messages. The application error boundary logs the diagnostic and selects a known localized message that accurately describes which state was retained or committed.

`zh_CN.po` is the only authoritative wording file. The current source format is singular, context-free gettext entries with UTF-8/JSON-compatible quoted escapes. Native Godot owns parsing at runtime. The check-only source guard rejects duplicates, empty/fuzzy/obsolete messages and malformed entries before Godot can discard or normalize them. Add plural/context source-guard support only when a real additional language/content requirement needs it; do not implement a runtime PO parser.

## Verification and packaging

Run:

```sh
bash tools/godot/localization-checks/run.sh
bash tools/godot/localization-checks/run.sh --packaged
```

Both operate in fresh temporary native projects/data directories. The packaged variant exports and launches an isolated Linux release harness. It verifies every catalog entry through `TranslationServer`, native resource loading, exact Chinese text, formatting and missing-key behavior from the exported package with no source files/editor import cache beside the executable.

A product package is accepted only when its own release diagnostics load this resource and validate its required UI/content keys. The isolated harness proves the resource/formatter/export contract, not the game's currently unmigrated consumers, save schemas, fonts, screen layout, or complete locale-independent determinism. Hook this check into the main gate and the product release diagnostic when integration is owned and ready. The root injects the same service into narrative, menu, staging and semantic-refusal presentation consumers. Unreadable-save details remain application diagnostics; the player sees a localized notice that the original file was retained. Missing-art details remain available in `RenderingDiagnostics`, while controls display one localized artwork-failure notice. Native story/staging schemas are version 2 with no old-field fallback. `validate_narrative.py` validates the full native content-key inventory; 91 beats use manually authored semantic identities, never array positions or hashes. Run `python3 -m unittest discover -s tools/godot/localization-checks -p "test_*.py"` for the parser and authoring negative controls.
