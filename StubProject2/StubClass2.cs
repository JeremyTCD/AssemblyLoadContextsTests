using System;

namespace StubProject2
{
    public class StubClass2
    {
        public int StubInstanceField = 0;

        public int StubFieldProduct(StubClass2 secondary)
        {
            return StubInstanceField * secondary.StubInstanceField;
        }
    }
}
