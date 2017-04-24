using Xunit;
using Atamo;

namespace Atamo.UnitTests.Service
{
    public class AtamoService_IsPrimeShould
    {
        private readonly ServiceD _primeService;

        public AtamoService_IsPrimeShould()
        {
            _primeService = new ServiceD();
        }

        [Fact]
        public void ReturnFalseGivenValueOf1()
        {
            var result = _primeService.IsPrime(1);

            Assert.False(result, $"1 should not be prime");
        }
    }
}