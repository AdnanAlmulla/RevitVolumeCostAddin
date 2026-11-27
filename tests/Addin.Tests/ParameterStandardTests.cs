using System.Collections.Generic;
using System.Linq;
using Addin.Standard;
using Xunit;

namespace Addin.Tests
{
    /// <summary>
    /// The parameter standard the Parameter standard button shows, as agreed with the technicians on 27 Nov 2026
    /// (website repo, docs/Revit parameter standard.xlsx). A change to the standard changes these lists on purpose.
    /// </summary>
    public class ParameterStandardTests
    {
        private static StandardParameter Named(string name)
        {
            return ParameterStandard.PARAMETERS.Single(parameter => parameter.Name == name);
        }

        [Fact]
        public void The_five_agreed_parameters_are_listed_once_each()
        {
            string[] names = ParameterStandard.PARAMETERS.Select(parameter => parameter.Name).ToArray();

            Assert.Equal(new[] { "Division_Shared", "Concrete Grade_Shared", "Structural System_Shared", "Wall Function_Shared", "Steel Grade_Shared" }, names);
        }

        [Fact]
        public void Divisions_include_Roof()
        {
            Assert.Equal(new[] { "Piles", "Foundation", "Basement", "Ground Floor", "Podium", "Tower", "Roof" }, Named("Division_Shared").AllowedValues);
        }

        [Fact]
        public void Wall_functions_include_Upstand_and_a_blank_is_a_plain_wall()
        {
            StandardParameter wallFunction = Named("Wall Function_Shared");

            Assert.Equal(new[] { "Retaining", "Shear", "Core", "Upstand" }, wallFunction.AllowedValues);
            Assert.Equal("Wall (a plain wall)", wallFunction.IfBlank);
        }

        [Fact]
        public void A_blank_structural_system_is_RC()
        {
            StandardParameter structuralSystem = Named("Structural System_Shared");

            Assert.Equal(new[] { "RC", "PT" }, structuralSystem.AllowedValues);
            Assert.Equal("RC", structuralSystem.IfBlank);
        }

        [Fact]
        public void Concrete_grades_include_Blinding_and_grades_allow_other_classes_in_the_same_format()
        {
            Assert.Contains("Blinding", Named("Concrete Grade_Shared").AllowedValues);
            Assert.NotNull(Named("Concrete Grade_Shared").OtherValuesNote);
            Assert.NotNull(Named("Steel Grade_Shared").OtherValuesNote);
        }

        [Fact]
        public void No_value_carries_spaces_at_either_end_the_website_matches_text_exactly()
        {
            IEnumerable<string> everyValue = ParameterStandard.PARAMETERS.SelectMany(parameter => parameter.AllowedValues);

            Assert.All(everyValue, value => Assert.Equal(value.Trim(), value));
        }

        [Fact]
        public void Every_parameter_is_bound_to_at_least_one_category()
        {
            Assert.All(ParameterStandard.PARAMETERS, parameter => Assert.NotEmpty(parameter.Categories));
        }
    }
}
