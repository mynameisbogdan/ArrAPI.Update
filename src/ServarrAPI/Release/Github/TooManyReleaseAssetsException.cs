using System;

namespace ServarrAPI.Release.Github;

public class TooManyReleaseAssetsException(string message)
    : Exception(message)
{
}
