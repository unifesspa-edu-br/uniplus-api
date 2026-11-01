namespace Unifesspa.UniPlus.Configuracao.API.Contracts.Requests;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>Um item do catálogo que o administrador escolheu enviar. Declara no contrato o vocabulário do cadastro.</summary>
public sealed record ItemParaSolicitarRequest(
    [property: VocabularioFechado(CadastrosDaProtecaoDoCatalogo.TipoDocumento, CadastrosDaProtecaoDoCatalogo.Fato)]
    string? Cadastro,
    Guid Id);
