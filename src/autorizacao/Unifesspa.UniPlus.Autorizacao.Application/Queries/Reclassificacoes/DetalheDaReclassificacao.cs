namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O detalhe da reclassificação: as permissões de cada versão em uso, com o caminho do documento instituidor da versão que o
/// tem, e as concessões suspensas com as etapas que a base de cada uma exige e as já decididas. O detalhe é o único caminho da
/// fila até a decisão.
/// </summary>
public sealed class DetalheDaReclassificacao
{
    private readonly IReclassificacaoRepository _reclassificacoes;
    private readonly IPerfilRepository _perfis;
    private readonly ConcessoesDaReclassificacao _concessoes;
    private readonly TimeProvider _clock;

    public DetalheDaReclassificacao(
        IReclassificacaoRepository reclassificacoes, IPerfilRepository perfis, ConcessoesDaReclassificacao concessoes, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(reclassificacoes);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(clock);

        _reclassificacoes = reclassificacoes;
        _perfis = perfis;
        _concessoes = concessoes;
        _clock = clock;
    }

    /// <summary>O detalhe da reclassificação para <paramref name="leitor"/>; recusa quando ela ou o perfil não existem.</summary>
    public async Task<Result<ReclassificacaoDto>> ObterAsync(Guid id, Identidade? leitor, CancellationToken cancellationToken)
    {
        ReclassificacaoDto? dto = await MontarAsync(id, leitor, cancellationToken).ConfigureAwait(false);

        return dto is null ? Result<ReclassificacaoDto>.Failure(ReclassificacaoErros.NaoEncontrada()) : Result<ReclassificacaoDto>.Success(dto);
    }

    private async Task<ReclassificacaoDto?> MontarAsync(Guid id, Identidade? leitor, CancellationToken cancellationToken)
    {
        ReclassificacaoDaPermissao? reclassificacao = await _reclassificacoes.ObterAsync(id, cancellationToken).ConfigureAwait(false);
        Perfil? perfil = reclassificacao is null
            ? null
            : await _perfis.ObterPorIdAsync(reclassificacao.PerfilId, cancellationToken).ConfigureAwait(false);

        return reclassificacao is null || perfil is null
            ? null
            : await DetalharAsync(reclassificacao, perfil, leitor, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReclassificacaoDto> DetalharAsync(
        ReclassificacaoDaPermissao reclassificacao, Perfil perfil, Identidade? leitor, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> emUso = await _reclassificacoes.ListarVersoesEmUsoAsync(perfil.Id, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ConcessaoSuspensa> suspensas = await _concessoes
            .LerSuspensasAsync(reclassificacao, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        return new ReclassificacaoDto(
            reclassificacao.Id,
            perfil.Id,
            perfil.Nome,
            reclassificacao.Codigo,
            Mudanca(reclassificacao),
            reclassificacao.Instante,
            await _reclassificacoes.EstaVigenteAsync(reclassificacao, cancellationToken).ConfigureAwait(false),
            [.. perfil.Versoes.Where(v => emUso.Contains(v.Id)).OrderByDescending(static v => v.Numero).Select(v => Versao(perfil, v))],
            [.. suspensas.Select(s => Concessao(perfil, reclassificacao, s, leitor))]);
    }

    internal static MudancaDaPermissaoDto Mudanca(ReclassificacaoDaPermissao r) => new(
        Texto(r.ClassificacaoAnterior), Texto(r.ClassificacaoNova), r.DeUnidadeAnterior, r.DeUnidadeNovo);

    internal static string Texto(ClassificacaoDeProtecao classificacao) => classificacao.ToString().ToLowerInvariant();

    // O documento instituidor é o da própria versão ou, na versão de remoção, o da proposta que ela herda.
    private static VersaoEmUsoDto Versao(Perfil perfil, VersaoDoPerfil versao)
    {
        Guid portadora = versao.VersaoDoDocumento ?? versao.Id;
        bool temDocumento = perfil.Versoes.Any(v => v.Id == portadora && v.Documentos.Count > 0);

        return new VersaoEmUsoDto(
            versao.Id,
            versao.Numero,
            versao.Situacao.ToCodigo(),
            [.. versao.Permissoes.OrderBy(static p => p.Codigo, StringComparer.Ordinal)
                .Select(static p => new PermissaoDaVersaoEmUsoDto(p.Codigo, Texto(p.Classificacao), p.DeUnidade))],
            temDocumento ? $"/api/autorizacao/perfis/{perfil.Id:D}/versoes/{portadora:D}/documento" : null);
    }

    private static ConcessaoSuspensaDto Concessao(Perfil perfil, ReclassificacaoDaPermissao reclassificacao, ConcessaoSuspensa suspensa, Identidade? leitor)
    {
        Concessao concessao = suspensa.Item.Concessao;

        return new ConcessaoSuspensaDto(
            concessao.Id,
            concessao.BeneficiarioNome,
            concessao.EscopoTipo.ToCodigo(),
            concessao.UnidadeId,
            perfil.Versoes.FirstOrDefault(v => v.Id == concessao.VersaoId)?.Numero,
            [.. reclassificacao.EtapasExigidas(suspensa.Item.Base).Select(static e => e.ToCodigo())],
            [.. suspensa.Decisoes.Select(static d => d.Etapa.ToCodigo())],
            leitor is not null && concessao.Beneficiario.Equals(leitor));
    }
}
