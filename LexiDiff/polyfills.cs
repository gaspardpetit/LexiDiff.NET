namespace System.Runtime.CompilerServices
{
	internal sealed class IsExternalInit { }
	[AttributeUsage(AttributeTargets.Method)]
	internal sealed class ModuleInitializerAttribute : Attribute { }
	[AttributeUsage(AttributeTargets.Parameter)]
	internal sealed class CallerArgumentExpressionAttribute : Attribute
	{
		public CallerArgumentExpressionAttribute(string paramName) { }
	}
}
namespace System.Diagnostics.CodeAnalysis
{
	[AttributeUsage(AttributeTargets.Parameter)]
	internal sealed class NotNullWhenAttribute : Attribute { public NotNullWhenAttribute(bool r) { } }
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	internal sealed class MaybeNullAttribute : Attribute { }
	[AttributeUsage(AttributeTargets.Method)]
	internal sealed class DoesNotReturnAttribute : Attribute { }
}
