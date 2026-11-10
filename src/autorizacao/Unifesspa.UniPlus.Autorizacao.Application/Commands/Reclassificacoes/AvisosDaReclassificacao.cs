namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Os avisos dos fatos da reclassificação do catálogo. A subida de classe avisa quem aprova pela proteção de dados; a
/// mudança de escopo e a divergência da governança avisam esse grupo e quem mantém perfis. A descida não avisa: fica só
/// na trilha. O conteúdo é o modelo fixo, sem dado pessoal nem texto livre.
/// </summary>
public sealed class AvisosDaReclassificacao
{
    private static readonly GrupoDaGovernanca[] DosDoisGrupos = [GrupoDaGovernanca.ProtecaoDeDados, GrupoDaGovernanca.Manutencao];

    private readonly EmissorDeAvisos _emissor;
    private readonly GruposDaGovernanca _grupos;
    private readonly ILinkDaGovernanca _link;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly IReclassificacaoRepository _reclassificacoes;

    public AvisosDaReclassificacao(
        EmissorDeAvisos emissor,
        GruposDaGovernanca grupos,
        ILinkDaGovernanca link,
        CatalogoDaAplicacao catalogo,
        IReclassificacaoRepository reclassificacoes)
    {
        ArgumentNullException.ThrowIfNull(emissor);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(reclassificacoes);

        _emissor = emissor;
        _grupos = grupos;
        _link = link;
        _catalogo = catalogo;
        _reclassificacoes = reclassificacoes;
    }

    /// <summary>
    /// Monta os e-mails do fato, ou nenhum quando o fato não pede aviso ou não existe mais. Devolve nulo quando o fato não
    /// foi encontrado.
    /// </summary>
    public async Task<IReadOnlyList<EnviarEmail>?> MontarAsync(FatoSemAviso fato, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fato);

        FatoDoAviso? descrito = await DescreverAsync(fato, cancellationToken).ConfigureAwait(false);
        if (descrito is null)
        {
            return null;
        }

        IReadOnlyDictionary<Guid, string> nomes = await _reclassificacoes
            .ObterNomesDosPerfisAsync([descrito.PerfilId], cancellationToken).ConfigureAwait(false);

        return nomes.TryGetValue(descrito.PerfilId, out string? nome)
            ? await EmitirAsync(fato.Id, descrito, nome, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private Task<FatoDoAviso?> DescreverAsync(FatoSemAviso fato, CancellationToken cancellationToken) =>
        fato.Origem == OrigemDoAviso.Reclassificacao
            ? DaReclassificacaoAsync(fato.Id, cancellationToken)
            : DaDivergenciaAsync(fato.Id, cancellationToken);

    private async Task<IReadOnlyList<EnviarEmail>> EmitirAsync(
        Guid fatoId, FatoDoAviso descrito, string perfilNome, CancellationToken cancellationToken)
    {
        ModeloDeAviso modelo = new(
            descrito.Tipo,
            perfilNome,
            null,
            [PerfilMapping.Descrever(descrito.Codigo, _catalogo).Nome],
            _link.DoPerfil(descrito.PerfilId),
            Mudanca: descrito.Mudanca);
        List<DestinatarioDoAviso> destinatarios = await _grupos.ListarAsync(descrito.Grupos, cancellationToken).ConfigureAwait(false);

        return await _emissor.EmitirAsync(new PedidoDeAviso(modelo, fatoId, string.Empty, destinatarios, []), cancellationToken).ConfigureAwait(false);
    }

    private async Task<FatoDoAviso?> DaReclassificacaoAsync(Guid id, CancellationToken cancellationToken)
    {
        ReclassificacaoDaPermissao? r = await _reclassificacoes.ObterAsync(id, cancellationToken).ConfigureAwait(false);
        if (r is null || r.Desceu && !r.MudouEscopo)
        {
            return null;
        }

        TipoDeAviso tipo = r.MudouEscopo ? TipoDeAviso.EscopoDaPermissaoAlterado : TipoDeAviso.PermissaoReclassificada;

        return new FatoDoAviso(r.PerfilId, r.Codigo, tipo, MudancaDe(r), r.MudouEscopo ? DosDoisGrupos : [GrupoDaGovernanca.ProtecaoDeDados]);
    }

    private async Task<FatoDoAviso?> DaDivergenciaAsync(Guid id, CancellationToken cancellationToken)
    {
        DivergenciaDaGovernanca? d = await _reclassificacoes.ObterDivergenciaAsync(id, cancellationToken).ConfigureAwait(false);

        return d is null
            ? null
            : new FatoDoAviso(d.PerfilId, d.Codigo, TipoDeAviso.DivergenciaDaGovernanca, $"divergência de {d.Tipo.ToCodigo()}", DosDoisGrupos);
    }

    private static string MudancaDe(ReclassificacaoDaPermissao r)
    {
        List<string> partes = [];
        if (r.Subiu)
        {
            partes.Add(ClassificacaoDe(r.ClassificacaoAnterior, r.ClassificacaoNova));
        }

        if (r.MudouEscopo)
        {
            partes.Add(EscopoDe(r.DeUnidadeAnterior, r.DeUnidadeNovo));
        }

        return string.Join("; ", partes);
    }

    private static string ClassificacaoDe(ClassificacaoDeProtecao de, ClassificacaoDeProtecao para) =>
        $"classificação de {ValoresCanonicos.De(de)} para {ValoresCanonicos.De(para)}";

    private static string EscopoDe(bool deUnidadeAntes, bool deUnidadeAgora) =>
        $"escopo de {Escopo(deUnidadeAntes)} para {Escopo(deUnidadeAgora)}";

    private static string Escopo(bool deUnidade) => deUnidade ? "de unidade" : "institucional";

    private sealed record FatoDoAviso(Guid PerfilId, string Codigo, TipoDeAviso Tipo, string Mudanca, IReadOnlyList<GrupoDaGovernanca> Grupos);
}
