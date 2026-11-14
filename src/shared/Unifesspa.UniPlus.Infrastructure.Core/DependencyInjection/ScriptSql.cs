namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>Um script SQL embutido no assembly do módulo, identificado pelo nome do recurso.</summary>
internal sealed record ScriptSql(string Nome, string Texto);
