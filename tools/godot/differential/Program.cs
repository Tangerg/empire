using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
var input = args[0];
var method = typeof(StateDigest).Assembly.GetType("Empire.Core.Determinism.EcmaNumber")!.GetMethod("Format",BindingFlags.Static|BindingFlags.NonPublic)!;
var format = method.CreateDelegate<Func<double,string>>();
var count = 0;
foreach (var line in File.ReadLines(Path.Combine(input,"numbers.tsv")))
{
    var fields = line.Split('\t');
    var value = BitConverter.UInt64BitsToDouble(ulong.Parse(fields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    var formatted = format(value);
    if (formatted != fields[1]) throw new Exception($"Number text mismatch at bits {fields[0]}: {formatted} != {fields[1]}");
    var actual = StateDigest.Hash(new JsonObject { ["number"] = value });
    if (actual != fields[2]) throw new Exception($"Constructed number digest mismatch at bits {fields[0]}: {actual} != {fields[2]}");
    actual = StateDigest.Hash(JsonNode.Parse("{\"number\":" + fields[1] + "}")!);
    if (actual != fields[2]) throw new Exception($"Parsed number digest mismatch at bits {fields[0]}: {actual} != {fields[2]}");
    count++;
}
Console.WriteLine($"PASS {count} finite binary64 values: exact ECMAScript text, constructed and parsed state digests");
count = 0;
foreach (var line in File.ReadLines(Path.Combine(input,"strings.jsonl")))
{
    var vector = JsonNode.Parse(line)!;
    var actual = StateDigest.Hash(vector["value"]!);
    var expected = vector["hash"]!.GetValue<string>();
    if (actual != expected) throw new Exception($"String digest mismatch for vector {count}: {actual} != {expected}");
    count++;
}
Console.WriteLine($"PASS {count} UTF-16 string and canonical path vectors");
foreach (var value in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
{
    try { StateDigest.Hash(new JsonObject { ["number"] = value }); }
    catch (DomainInvariantException) { continue; }
    throw new Exception("Nonfinite state number was accepted");
}
Console.WriteLine("PASS nonfinite numbers rejected");
var rng = new SplitMixRandom();
count = 0;
foreach (var line in File.ReadLines(Path.Combine(input,"rng.jsonl")))
{
    var vector = JsonNode.Parse(line)!;
    var state = RandomState.Create(vector["seed"]!.GetValue<double>());
    var stream = vector["stream"]!.GetValue<string>();
    state.Counters[stream] = vector["counter"]!.GetValue<double>();
    var expected = vector["value"]!.GetValue<double>();
    if (rng.Peek(state,stream) != expected || rng.Next(state,stream) != expected) throw new Exception($"RNG mismatch at vector {count}");
    if (state.Counters[stream] != vector["nextCounter"]!.GetValue<double>()) throw new Exception($"RNG counter mismatch at vector {count}");
    count++;
}
Console.WriteLine($"PASS {count} RNG seed/counter edge vectors");
void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
void Invariant(Action action) { try { action(); } catch (DomainInvariantException) { return; } throw new Exception("Expected DomainInvariantException"); }
var boundaryState = RandomState.Create(-1.75);
Require(boundaryState.Seed == uint.MaxValue, "Fractional negative seed coercion");
foreach (var seed in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.0 })
    Require(RandomState.Create(seed).Seed == 0, "Nonfinite or zero seed coercion");
Require(!rng.Chance(boundaryState,"chance",0) && rng.Chance(boundaryState,"chance",100), "Certain chance result");
Require(boundaryState.Counters.Count == 0, "Certain chance advanced stream");
Require(!rng.Chance(boundaryState,"chance",double.NaN) && boundaryState.Counters["chance"] == 1, "NaN chance source semantics");
foreach (var bound in new[] {0.0,-1,1.5,double.NaN,double.PositiveInfinity}) Invariant(() => rng.Int(boundaryState,"invalid",bound));
Invariant(() => rng.Pick<int>(boundaryState,"invalid",Array.Empty<int>()));
Require(!boundaryState.Counters.ContainsKey("invalid"), "Invalid operation advanced stream");
boundaryState.Counters["saved"] = 4294967296;
var serialized = JsonSerializer.Serialize(boundaryState);
var restored = JsonSerializer.Deserialize<RandomState>(serialized)!;
Require(restored.Seed == boundaryState.Seed && restored.Counters["saved"] == 4294967296, "RandomState JSON round trip");
Require(rng.Next(restored,"saved") == rng.Next(boundaryState,"saved"), "Reloaded RNG diverged");
var clone = new RandomState(boundaryState.Seed,boundaryState.Counters);
clone.Counters["saved"] = 7;
Require(boundaryState.Counters["saved"] == 4294967297, "RandomState clone shares counters");
var exact = new DeterministicOnlyRandom();
Invariant(() => exact.Next(boundaryState,"exact"));
Invariant(() => exact.Peek(boundaryState,"exact"));
Invariant(() => exact.Int(boundaryState,"exact",1));
Invariant(() => exact.Int(boundaryState,"exact",1.0));
Invariant(() => exact.Chance(boundaryState,"exact",0));
Invariant(() => exact.Pick(boundaryState,"exact",new[]{1}));
Require(!boundaryState.Counters.ContainsKey("exact"), "Exact source advanced stream");
Console.WriteLine("PASS RNG validation, chance endpoints, exact-source refusal, clone independence and JSON round trip");
