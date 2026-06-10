using Microsoft.Extensions.DependencyInjection;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Alabama;
using PaycheckCalc.Core.Tax.Arizona;
using PaycheckCalc.Core.Tax.Arkansas;
using PaycheckCalc.Core.Tax.California;
using PaycheckCalc.Core.Tax.Colorado;
using PaycheckCalc.Core.Tax.Connecticut;
using PaycheckCalc.Core.Tax.Delaware;
using PaycheckCalc.Core.Tax.DistrictOfColumbia;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Georgia;
using PaycheckCalc.Core.Tax.Hawaii;
using PaycheckCalc.Core.Tax.Idaho;
using PaycheckCalc.Core.Tax.Illinois;
using PaycheckCalc.Core.Tax.Indiana;
using PaycheckCalc.Core.Tax.Iowa;
using PaycheckCalc.Core.Tax.Kansas;
using PaycheckCalc.Core.Tax.Kentucky;
using PaycheckCalc.Core.Tax.Louisiana;
using PaycheckCalc.Core.Tax.Maine;
using PaycheckCalc.Core.Tax.Maryland;
using PaycheckCalc.Core.Tax.Massachusetts;
using PaycheckCalc.Core.Tax.Michigan;
using PaycheckCalc.Core.Tax.Minnesota;
using PaycheckCalc.Core.Tax.Mississippi;
using PaycheckCalc.Core.Tax.Missouri;
using PaycheckCalc.Core.Tax.Montana;
using PaycheckCalc.Core.Tax.Nebraska;
using PaycheckCalc.Core.Tax.NewJersey;
using PaycheckCalc.Core.Tax.NewMexico;
using PaycheckCalc.Core.Tax.NewYork;
using PaycheckCalc.Core.Tax.NorthCarolina;
using PaycheckCalc.Core.Tax.NorthDakota;
using PaycheckCalc.Core.Tax.Ohio;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.Oregon;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.RhodeIsland;
using PaycheckCalc.Core.Tax.SouthCarolina;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.Utah;
using PaycheckCalc.Core.Tax.Vermont;
using PaycheckCalc.Core.Tax.Virginia;
using PaycheckCalc.Core.Tax.Washington;
using PaycheckCalc.Core.Tax.WestVirginia;
using PaycheckCalc.Core.Tax.Wisconsin;
using PaycheckCalc.Core.Tax.Wyoming;

namespace PaycheckCalc.Core.DependencyInjection;

public static class PaycheckCoreServiceCollectionExtensions
{
    public static IServiceCollection AddPaycheckCalcCore(
        this IServiceCollection services,
        ITaxDataReader dataReader)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataReader);

        var irs15tJson = dataReader.ReadAllText("us_irs_15t_2026_percentage_automated.json");
        var arJson     = dataReader.ReadAllText("ar_withholding_2026.json");
        var okJson     = dataReader.ReadAllText("ok_ow2_2026_percentage.json");
        var caJson     = dataReader.ReadAllText("ca_method_b_2026.json");
        var coJson     = dataReader.ReadAllText("co_dr0004_2026.json");
        var ctJson     = dataReader.ReadAllText("connecticut_withholding_2026.json");

        var schemaJsonMap = new Dictionary<UsState, string>();
        foreach (var state in Enum.GetValues<UsState>())
        {
            var name = state.ToString().ToLowerInvariant();
            try
            {
                schemaJsonMap[state] = dataReader.ReadAllText($"schemas/{name}.json");
            }
            catch (FileNotFoundException)
            {
                // No schema file for this state — provider returns empty schema.
            }
        }
        var schemaProvider = new JsonStateSchemaProvider(schemaJsonMap);
        services.AddSingleton<IStateSchemaProvider>(schemaProvider);

        services.AddSingleton(dataReader);

        var fica = new FicaCalculator();
        services.AddSingleton(fica);

        var irs15t = new Irs15TPercentageCalculator(irs15tJson);
        services.AddSingleton(irs15t);

        var arFormulaCalc = new ArkansasFormulaCalculator(arJson);
        var caPercentCalc = new CaliforniaPercentageCalculator(caJson);
        var coCalc        = new ColoradoWithholdingCalculator(coJson, schemaProvider);
        var ctCalc        = new ConnecticutWithholdingCalculator(ctJson, schemaProvider);
        var okCalc        = new OklahomaOw2PercentageCalculator(okJson);

        services.AddSingleton(arFormulaCalc);
        services.AddSingleton(caPercentCalc);
        services.AddSingleton(coCalc);
        services.AddSingleton(ctCalc);
        services.AddSingleton(okCalc);

        var stateRegistry = new StateCalculatorRegistry();
        stateRegistry.Register(new AlabamaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new ArizonaWithholdingCalculator());
        stateRegistry.Register(new ArkansasWithholdingCalculator(arFormulaCalc));
        stateRegistry.Register(new CaliforniaWithholdingCalculator(caPercentCalc, schemaProvider));
        stateRegistry.Register(coCalc);
        stateRegistry.Register(ctCalc);
        stateRegistry.Register(new DelawareWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new DistrictOfColumbiaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new GeorgiaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new HawaiiWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new IdahoWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new IllinoisWithholdingCalculator());
        stateRegistry.Register(new IndianaWithholdingCalculator());
        stateRegistry.Register(new IowaWithholdingCalculator());
        stateRegistry.Register(new KansasWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new KentuckyWithholdingCalculator());
        stateRegistry.Register(new LouisianaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MaineWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MarylandWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MassachusettsWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MichiganWithholdingCalculator());
        stateRegistry.Register(new MinnesotaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MississippiWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MissouriWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MontanaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NebraskaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NewJerseyWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NewMexicoWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NewYorkWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NorthCarolinaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NorthDakotaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new OhioWithholdingCalculator());
        stateRegistry.Register(new OregonWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new RhodeIslandWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new SouthCarolinaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new UtahWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new VermontWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new VirginiaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new WestVirginiaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new WisconsinWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new OklahomaWithholdingCalculator(okCalc, schemaProvider));
        stateRegistry.Register(new PennsylvaniaWithholdingCalculator());
        stateRegistry.Register(new WashingtonWithholdingCalculator());
        stateRegistry.Register(new WyomingWithholdingCalculator());

        UsState[] noTaxStates =
        [
            UsState.AK, UsState.FL, UsState.NV, UsState.NH,
            UsState.SD, UsState.TN, UsState.TX
        ];
        foreach (var state in noTaxStates)
            stateRegistry.Register(new NoIncomeTaxWithholdingAdapter(state));

        foreach (var (state, config) in StateTaxConfigs2026.Configs)
            stateRegistry.Register(new PercentageMethodWithholdingAdapter(state, config, schemaProvider));

        services.AddSingleton(stateRegistry);

        services.AddSingleton(new PayCalculator(stateRegistry, fica, irs15t));
        services.AddSingleton(new AnnualProjectionCalculator(irs15t, fica));

        return services;
    }
}
