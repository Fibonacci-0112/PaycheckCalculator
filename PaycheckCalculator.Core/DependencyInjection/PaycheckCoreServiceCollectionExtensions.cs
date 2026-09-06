using Microsoft.Extensions.DependencyInjection;
using PaycheckCalculator.Core.Budgeting;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Alabama;
using PaycheckCalculator.Core.Tax.Arizona;
using PaycheckCalculator.Core.Tax.Arkansas;
using PaycheckCalculator.Core.Tax.California;
using PaycheckCalculator.Core.Tax.Colorado;
using PaycheckCalculator.Core.Tax.Connecticut;
using PaycheckCalculator.Core.Tax.Delaware;
using PaycheckCalculator.Core.Tax.DistrictOfColumbia;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Georgia;
using PaycheckCalculator.Core.Tax.Hawaii;
using PaycheckCalculator.Core.Tax.Idaho;
using PaycheckCalculator.Core.Tax.Illinois;
using PaycheckCalculator.Core.Tax.Indiana;
using PaycheckCalculator.Core.Tax.Iowa;
using PaycheckCalculator.Core.Tax.Kansas;
using PaycheckCalculator.Core.Tax.Kentucky;
using PaycheckCalculator.Core.Tax.Louisiana;
using PaycheckCalculator.Core.Tax.Maine;
using PaycheckCalculator.Core.Tax.Maryland;
using PaycheckCalculator.Core.Tax.Massachusetts;
using PaycheckCalculator.Core.Tax.Michigan;
using PaycheckCalculator.Core.Tax.Minnesota;
using PaycheckCalculator.Core.Tax.Mississippi;
using PaycheckCalculator.Core.Tax.Missouri;
using PaycheckCalculator.Core.Tax.Montana;
using PaycheckCalculator.Core.Tax.Nebraska;
using PaycheckCalculator.Core.Tax.NewJersey;
using PaycheckCalculator.Core.Tax.NewMexico;
using PaycheckCalculator.Core.Tax.NewYork;
using PaycheckCalculator.Core.Tax.NorthCarolina;
using PaycheckCalculator.Core.Tax.NorthDakota;
using PaycheckCalculator.Core.Tax.Ohio;
using PaycheckCalculator.Core.Tax.Oklahoma;
using PaycheckCalculator.Core.Tax.Oregon;
using PaycheckCalculator.Core.Tax.Pennsylvania;
using PaycheckCalculator.Core.Tax.RhodeIsland;
using PaycheckCalculator.Core.Tax.SouthCarolina;
using PaycheckCalculator.Core.Tax.State;
using PaycheckCalculator.Core.Tax.Supplemental;
using PaycheckCalculator.Core.Tax.Sources;
using PaycheckCalculator.Core.Tax.Utah;
using PaycheckCalculator.Core.Tax.Vermont;
using PaycheckCalculator.Core.Tax.Virginia;
using PaycheckCalculator.Core.Tax.Washington;
using PaycheckCalculator.Core.Tax.WestVirginia;
using PaycheckCalculator.Core.Tax.Wisconsin;
using PaycheckCalculator.Core.Tax.Wyoming;

namespace PaycheckCalculator.Core.DependencyInjection;

public static class PaycheckCoreServiceCollectionExtensions
{
    public static IServiceCollection AddPaycheckCalculatorCore(
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
        var suppJson   = dataReader.ReadAllText("state_supplemental_2026.json");
        var assessJson = dataReader.ReadAllText("state_payroll_assessments_2026.json");
        var sourceManifestJson = dataReader.ReadAllText("tax_source_manifest_2026.json");
        var sourceCatalog = TaxSourceCatalog.Load(sourceManifestJson);

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
        var assessments = StatePayrollAssessments.Load(assessJson);
        services.AddSingleton<IStateSchemaProvider>(schemaProvider);
        services.AddSingleton(assessments);

        services.AddSingleton(dataReader);
        services.AddSingleton(sourceCatalog);

        var fica = new FicaCalculator();
        services.AddSingleton(fica);

        var irs15t = new Irs15TPercentageCalculator(irs15tJson);
        services.AddSingleton(irs15t);

        var arFormulaCalc = new ArkansasFormulaCalculator(arJson);
        var caPercentCalc = new CaliforniaPercentageCalculator(caJson);
        var coCalc        = new ColoradoWithholdingCalculator(coJson, schemaProvider, assessments);
        var ctCalc        = new ConnecticutWithholdingCalculator(ctJson, schemaProvider, assessments);
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
        stateRegistry.Register(new HawaiiWithholdingCalculator(schemaProvider, assessments));
        stateRegistry.Register(new IdahoWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new IllinoisWithholdingCalculator());
        stateRegistry.Register(new IndianaWithholdingCalculator());
        stateRegistry.Register(new IowaWithholdingCalculator());
        stateRegistry.Register(new KansasWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new KentuckyWithholdingCalculator());
        stateRegistry.Register(new LouisianaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MaineWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MarylandWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MassachusettsWithholdingCalculator(schemaProvider, assessments));
        stateRegistry.Register(new MichiganWithholdingCalculator());
        stateRegistry.Register(new MinnesotaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MississippiWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MissouriWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new MontanaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NebraskaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NewJerseyWithholdingCalculator(schemaProvider, assessments));
        stateRegistry.Register(new NewMexicoWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NewYorkWithholdingCalculator(schemaProvider, assessments));
        stateRegistry.Register(new NorthCarolinaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new NorthDakotaWithholdingCalculator(schemaProvider));
        stateRegistry.Register(new OhioWithholdingCalculator());
        stateRegistry.Register(new OregonWithholdingCalculator(schemaProvider, assessments));
        stateRegistry.Register(new RhodeIslandWithholdingCalculator(schemaProvider, assessments));
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

        sourceCatalog.ValidateCalculatorRegistrations(stateRegistry);
        services.AddSingleton(stateRegistry);

        var payCalculator = new PayCalculator(stateRegistry, fica, irs15t, sourceCatalog);
        services.AddSingleton(payCalculator);
        services.AddSingleton(new AnnualProjectionCalculator(irs15t, fica));
        services.AddSingleton(new GrossUpCalculator(payCalculator));
        services.AddSingleton(new SelfEmploymentCalculator(stateRegistry, fica, sourceCatalog));
        services.AddSingleton(new HourlySalaryCalculator());

        var federalSupplemental = new FederalSupplementalCalculator();
        var stateSupplemental = new StateSupplementalCalculator(suppJson);
        services.AddSingleton(federalSupplemental);
        services.AddSingleton(stateSupplemental);
        services.AddSingleton(new BonusCalculator(federalSupplemental, fica, stateSupplemental, sourceCatalog));

        services.AddSingleton(new BudgetCalculator());
        services.AddSingleton(new BudgetReportCalculator());

        return services;
    }
}
