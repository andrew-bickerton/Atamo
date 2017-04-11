using Xunit;
using Atamo.Service;

namespace Atamo.UnitTests.Service
{
    public class AtamoService_IsPrimeShould
    {
        private readonly AtamoService _primeService;

        public AtamoService_IsPrimeShould()
        {
            _primeService = new AtamoService();
        }

        [Fact]
        public void ReturnFalseGivenValueOf1()
        {
            var result = _primeService.IsPrime(1);

            Assert.False(result, $"1 should not be prime");
        }
    }
}