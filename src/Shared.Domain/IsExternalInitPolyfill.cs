#if NETSTANDARD2_0
// Polyfill for record 'init' accessors when targeting netstandard2.0
namespace System.Runtime.CompilerServices
{
	internal static class IsExternalInit { }
}
#endif
