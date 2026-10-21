using System.Text.Json.Nodes;
using Empire.Core.Resources;

internal static class SupportChecks
{
    public static void Resources()
    {
        var player = JsonNode.Parse("{\"id\":1,\"resources\":{\"funds\":{\"current\":5,\"capacity\":10}}}")!.AsObject();
        var resources = new BattleResources(new BattleResourceRules(DefaultResourcePolicies.Adapters.Clone(), DefaultResourcePolicies.Subjects.Clone()));
        var subject = new BattleResources.Subject("player", player);
        if (resources.Credit(subject, "funds", 12) != 5 || resources.Balance(subject, "funds") != 10)
            throw new Exception("Credit must clamp at capacity");
        if (resources.Spend(subject, "funds", 2.5) != 3 || resources.Balance(subject, "funds") != 7)
            throw new Exception("Spend must use JavaScript rounding");
        var costs = JsonNode.Parse("[{\"resource\":\"funds\",\"amount\":4},{\"resource\":\"funds\",\"amount\":4}]")!.AsArray();
        if (resources.CanAfford(subject, costs))
            throw new Exception("Costs must aggregate before affordability");
        try
        {
            resources.SpendAll(subject, costs);
            throw new Exception("Overspend accepted");
        }
        catch (Empire.Core.DomainInvariantException) { }
        if (resources.Balance(subject, "funds") != 7)
            throw new Exception("Failed aggregate spend changed account");
    }
}
