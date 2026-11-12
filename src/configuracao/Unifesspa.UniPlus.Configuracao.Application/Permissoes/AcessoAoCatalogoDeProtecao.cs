namespace Unifesspa.UniPlus.Configuracao.Application.Permissoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que quem age pode no circuito de aprovação da proteção de dados do catálogo: manter os tipos de documento, manter os
/// fatos, aprovar e consultar. A solicitação e a consulta alcançam os dois cadastros de uma vez, então o acesso é decidido
/// item a item por permissão, e não por um único atributo no endpoint. Quem decide é o próprio handler: nenhum emissor
/// consegue entregar o acesso pronto. A consulta só lê: não abre a solicitação nem a decisão.
/// </summary>
public readonly record struct AcessoAoCatalogoDeProtecao
{
    private AcessoAoCatalogoDeProtecao(DecisaoDeAcesso tipos, DecisaoDeAcesso fatos, DecisaoDeAcesso aprovacao, DecisaoDeAcesso consulta)
    {
        Tipos = tipos;
        Fatos = fatos;
        Aprovacao = aprovacao;
        Consulta = consulta;
    }

    public bool ManterTipos => Tipos is DecisaoPermitida;

    public bool ManterFatos => Fatos is DecisaoPermitida;

    public bool Aprovar => Aprovacao is DecisaoPermitida;

    /// <summary>Consulta a proteção do catálogo, como o Encarregado de dados, sem decidir nem solicitar.</summary>
    public bool Consultar => Consulta is DecisaoPermitida;

    public bool Algum => ManterTipos || ManterFatos || Aprovar || Consultar;

    /// <summary>Vê os dois cadastros: quem aprova ou quem consulta a proteção do catálogo.</summary>
    public bool VeOsDoisCadastros => Aprovar || Consultar;

    public static DomainError SemPermissao { get; } = new(
        ProtecaoDoCatalogoErrorCodes.SemPermissaoNoCatalogo,
        "Sem permissão para manter os tipos de documento, manter os fatos, aprovar ou consultar a proteção de dados do catálogo.");

    public static DomainError IdentidadeIncompleta { get; } = new(
        ProtecaoDoCatalogoErrorCodes.IdentidadeIncompleta,
        "O token não identifica o solicitante por completo; autentique-se de novo.");

    private DecisaoDeAcesso? Tipos { get; }

    private DecisaoDeAcesso? Fatos { get; }

    private DecisaoDeAcesso? Aprovacao { get; }

    private DecisaoDeAcesso? Consulta { get; }

    // Da permissão de classe mais alta para a mais baixa: é a primeira permitida que a leitura registra.
    private DecisaoDeAcesso[] Avaliadas => [.. new[] { Aprovacao, Consulta, Tipos, Fatos }.OfType<DecisaoDeAcesso>()];

    /// <summary>
    /// Avalia as quatro permissões sem registrar e, se nenhuma delas autoriza o pedido, grava as negativas pela política de
    /// registro. O token que não identifica o solicitante recusa com a identidade incompleta, e não com a falta de
    /// permissão.
    /// </summary>
    public static async Task<Result<AcessoAoCatalogoDeProtecao>> VerificarAsync(
        IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso tipos = await AvaliarAsync(verificador, PermissaoConfiguracao.TiposDocumentoManter, ResourceTiposConfiguracao.TipoDocumento, cancellationToken).ConfigureAwait(false);
        if (tipos is SemSujeito)
        {
            return Result<AcessoAoCatalogoDeProtecao>.Failure(IdentidadeIncompleta);
        }

        AcessoAoCatalogoDeProtecao acesso = await AvaliarAsDemaisAsync(verificador, tipos, cancellationToken).ConfigureAwait(false);
        await acesso.RegistrarSemNenhumaAsync(verificador, cancellationToken).ConfigureAwait(false);

        return Result<AcessoAoCatalogoDeProtecao>.Success(acesso);
    }

    /// <summary>
    /// Registra a leitura da fila pela permissão de classe mais alta que a autorizou: aprovar e consultar a proteção são
    /// pessoais e gravam; manter um cadastro é interno e não grava. Sem nenhuma permissão, <see cref="VerificarAsync"/> já
    /// gravou as negativas.
    /// </summary>
    public Task RegistrarLeituraAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return Algum
            ? verificador.RegistrarDesfechoAsync(Avaliadas, IntencaoDoRegistro.Pedido, cancellationToken)
            : Task.CompletedTask;
    }

    /// <summary>
    /// Grava a negativa de cada cadastro que o pedido alcançava e a pessoa não mantém, quando o pedido é recusado por
    /// isso apesar de ela ter outra permissão do circuito. Sem nenhuma permissão, <see cref="VerificarAsync"/> já gravou
    /// as negativas.
    /// </summary>
    public Task RegistrarRecusaDosCadastrosAsync(
        IVerificadorDeAcesso verificador, bool tipos, bool fatos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso[] negadas = Algum
            ? [.. new[] { tipos ? Tipos : null, fatos ? Fatos : null }.OfType<DecisaoNegada>()]
            : [];

        return negadas.Length == 0
            ? Task.CompletedTask
            : verificador.RegistrarDesfechoAsync(negadas, IntencaoDoRegistro.Pedido, cancellationToken);
    }

    // Depois de manter os tipos, que já identificou o solicitante: manter os fatos, aprovar e consultar a proteção.
    private static async Task<AcessoAoCatalogoDeProtecao> AvaliarAsDemaisAsync(
        IVerificadorDeAcesso verificador, DecisaoDeAcesso tipos, CancellationToken cancellationToken) =>
        new(
            tipos,
            await AvaliarAsync(verificador, PermissaoConfiguracao.FatosCandidatoManter, ResourceTiposConfiguracao.FatoCandidato, cancellationToken).ConfigureAwait(false),
            await AvaliarAsync(verificador, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar, ResourceTiposConfiguracao.ProtecaoDoCatalogo, cancellationToken).ConfigureAwait(false),
            await AvaliarAsync(verificador, PermissaoConfiguracao.ProtecaoDoCatalogoConsultar, ResourceTiposConfiguracao.ProtecaoDoCatalogo, cancellationToken).ConfigureAwait(false));

    private Task RegistrarSemNenhumaAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        Algum ? Task.CompletedTask : verificador.RegistrarDesfechoAsync(Avaliadas, IntencaoDoRegistro.Pedido, cancellationToken);

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador, PermissaoConfiguracao permissao, string resourceTipo, CancellationToken cancellationToken)
    {
        PermissionRequirement requisito = permissao.Requisito;
        Result<ResourceContext> resource = ResourceContext.From(resourceTipo, requisito.Sensibilidade);

        return verificador.AvaliarAsync(requisito, resource.Value, cancellationToken);
    }
}
