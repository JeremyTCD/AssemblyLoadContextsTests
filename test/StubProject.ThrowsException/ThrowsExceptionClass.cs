using System;

namespace StubProject.ThrowsException
{
    public class ThrowsExceptionClass
    {
        public void ThrowException(string exceptionMessage)
        {
            throw new Exception(exceptionMessage);
        }
    }
}
