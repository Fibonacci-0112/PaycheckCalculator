using System.Collections.Generic;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class JsonStateSchemaProviderTest
{
    [Fact]
    public void ParsesAllFieldTypes()
    {
        const string json = """
            {
              "state": "XX",
              "fields": [
                { "key": "Pick",  "label": "Pick",  "type": "Picker",  "required": true, "default": "A", "options": ["A","B"] },
                { "key": "Int",   "label": "Int",   "type": "Integer", "default": 5 },
                { "key": "Money", "label": "Money", "type": "Decimal", "default": 1.25 },
                { "key": "Flag",  "label": "Flag",  "type": "Toggle",  "default": true },
                { "key": "Name",  "label": "Name",  "type": "Text",    "default": "hello" }
              ]
            }
            """;

        var provider = new JsonStateSchemaProvider(new Dictionary<UsState, string> { [UsState.WY] = json });
        var schema = provider.GetSchema(UsState.WY);

        Assert.Equal(5, schema.Count);
        Assert.Equal(StateFieldType.Picker, schema[0].FieldType);
        Assert.True(schema[0].IsRequired);
        Assert.Equal("A", schema[0].DefaultValue);
        Assert.NotNull(schema[0].Options);
        Assert.Equal(2, schema[0].Options!.Count);

        Assert.Equal(StateFieldType.Integer, schema[1].FieldType);
        Assert.Equal(5, schema[1].DefaultValue);

        Assert.Equal(StateFieldType.Decimal, schema[2].FieldType);
        Assert.IsType<decimal>(schema[2].DefaultValue);
        Assert.Equal(1.25m, schema[2].DefaultValue);

        Assert.Equal(StateFieldType.Toggle, schema[3].FieldType);
        Assert.Equal(true, schema[3].DefaultValue);

        Assert.Equal(StateFieldType.Text, schema[4].FieldType);
        Assert.Equal("hello", schema[4].DefaultValue);
    }

    [Fact]
    public void GetOptions_ReturnsPickerOptions()
    {
        const string json = """
            {
              "state": "XX",
              "fields": [
                { "key": "FilingStatus", "label": "Filing Status", "type": "Picker", "options": ["Single","Married"] }
              ]
            }
            """;
        var provider = new JsonStateSchemaProvider(new Dictionary<UsState, string> { [UsState.WY] = json });

        var options = provider.GetOptions(UsState.WY, "FilingStatus");

        Assert.Equal(new[] { "Single", "Married" }, options);
    }

    [Fact]
    public void GetOptions_ReturnsEmptyForMissingField()
    {
        var provider = new JsonStateSchemaProvider(new Dictionary<UsState, string>());
        Assert.Empty(provider.GetOptions(UsState.WY, "Anything"));
    }

    [Fact]
    public void GetSchema_ReturnsEmptyForUnknownState()
    {
        var provider = new JsonStateSchemaProvider(new Dictionary<UsState, string>());
        Assert.Empty(provider.GetSchema(UsState.OK));
    }

    [Fact]
    public void EmptyFields_ParsesAsEmptySchema()
    {
        const string json = """
            { "state": "AK", "fields": [] }
            """;
        var provider = new JsonStateSchemaProvider(new Dictionary<UsState, string> { [UsState.AK] = json });
        Assert.Empty(provider.GetSchema(UsState.AK));
    }

    [Fact]
    public void TestSchemas_LoadsFromCopiedBinFiles()
    {
        // Sanity-check the test-host loader: copy of Schemas/al.json should produce
        // Alabama's five filing statuses.
        var alOptions = TestSchemas.Provider.GetOptions(UsState.AL, "FilingStatus");
        Assert.Equal(5, alOptions.Count);
        Assert.Contains("Single", alOptions);
        Assert.Contains("Head of Family", alOptions);
    }
}
