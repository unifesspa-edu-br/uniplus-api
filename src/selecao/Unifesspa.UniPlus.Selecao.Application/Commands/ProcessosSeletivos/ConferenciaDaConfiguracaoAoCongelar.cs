namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// A configuração conferida, pronta para ser canonicalizada: a configuração apta a congelar, a entrada do
/// canonicalizador e os cadastros com que ela foi julgada.
/// </summary>
internal sealed record ConfiguracaoConferida(
    EntradaCanonicalizacao Entrada, ConfiguracaoAptaACongelar ConfiguracaoApta, CadastrosDaConfiguracao Cadastros);

/// <summary>O que a conferência leu dos cadastros vivos, uma vez, para julgar a configuração e canonicalizá-la.</summary>
internal sealed record LeiturasDoCongelamento(
    CatalogoVivo Catalogo,
    RecopiadoAoCongelar Recopiado,
    TimeZoneInfo Fuso,
    CadastrosDaConfiguracao Cadastros,
    IReadOnlyList<FieldError> CausasDaAprovacao);

/// <summary>
/// Confere a configuração que a publicação, a retificação de ato único e o fechamento da sessão congelam, numa ordem só
/// de recusa (ADR-0109 D5, ADR-0125): lê os cadastros, confere a aprovação da proteção, confere a configuração
/// (<see cref="ConfiguracaoAptaACongelar"/>) e confere o que só a operação sabe. Uma leitura que falha impede todo o resto,
/// porque sem ela não há configuração a julgar; a aprovação da proteção é regra sobre uma leitura que nunca falha, então
/// roda junto das demais, e a resposta traz as causas de todas: a aprovação, a configuração e a operação, nessa ordem. Nada é canonicalizado nem gravado enquanto houver causa. O que difere entre as operações vem da
/// <see cref="OrigemDoCongelamento"/>.
/// </summary>
public sealed class ConferenciaDaConfiguracaoAoCongelar(
    ConferenciaDoCatalogoEDaProtecao catalogos,
    RecopiaAoCongelar recopia,
    IResolvedorFusoInstitucional resolvedorFuso,
    ResolucaoDosTiposDeAtoUnicos tiposDeAto,
    LeituraDoContextoDeContagemDePrazos contextos,
    LeituraDosCorretoresDasEtapas corretores,
    ConferenciaDaOperacaoAoCongelar operacao)
{
    internal async Task<Result<ConfiguracaoConferida>> ConferirAsync(
        ProcessoSeletivo processo,
        EditalDeclarado edital,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem,
        CancellationToken cancellationToken)
    {
        Result<LeiturasDoCongelamento> lidos = await LerOsCadastrosAsync(processo, ato, origem, cancellationToken).ConfigureAwait(false);
        if (lidos.IsFailure)
        {
            return Result<ConfiguracaoConferida>.ValidationFailure(lidos.Errors);
        }

        Result<ConfiguracaoAptaACongelar> daConfiguracao = ConfiguracaoAptaACongelar.Conferir(EstadoDaConfiguracao.De(processo), lidos.Value!.Cadastros);
        OperacaoConferida daOperacao = await operacao
            .ConferirAsync(processo, edital, ato, origem, lidos.Value, cancellationToken)
            .ConfigureAwait(false);

        return Resultado(daConfiguracao, daOperacao, processo, ato, origem, lidos.Value);
    }

    // O fuso ausente é defeito de instalação (500), e não causa da configuração: recusa sozinho, sem ler os demais cadastros. O catálogo com a cópia refeita dos dados do item e os
    // tipos que a versão vigente já congelou são leituras independentes e acumulam as causas.
    private async Task<Result<LeiturasDoCongelamento>> LerOsCadastrosAsync(
        ProcessoSeletivo processo, AtoEDocumentoConferidos ato, OrigemDoCongelamento origem, CancellationToken cancellationToken)
    {
        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();

        return fuso.IsFailure
            ? Result<LeiturasDoCongelamento>.ValidationFailure(fuso.Errors)
            : await LerOsCadastrosComFusoAsync(processo, ato, origem, fuso.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<LeiturasDoCongelamento>> LerOsCadastrosComFusoAsync(
        ProcessoSeletivo processo, AtoEDocumentoConferidos ato, OrigemDoCongelamento origem, TimeZoneInfo fuso, CancellationToken cancellationToken)
    {
        Result<CatalogoERecopia> vivo = await LerOCatalogoEARecopiaAsync(processo, origem, cancellationToken).ConfigureAwait(false);
        Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> jaCongelados = origem.TiposJaCongelados();
        if (vivo.IsFailure || jaCongelados.IsFailure)
        {
            IReadOnlyList<FieldError> daAprovacao = vivo.IsSuccess ? vivo.Value!.CausasDaAprovacao : [];

            return Result<LeiturasDoCongelamento>.ValidationFailure([.. daAprovacao.Concat(vivo.Errors).Concat(jaCongelados.Errors).Distinct()]);
        }

        CadastrosDaConfiguracao cadastros = await LerOsCadastrosDoDominioAsync(processo, ato, jaCongelados.Value!, vivo.Value!, cancellationToken)
            .ConfigureAwait(false);

        return Result<LeiturasDoCongelamento>.Success(
            new LeiturasDoCongelamento(vivo.Value!.Catalogo, vivo.Value.Recopiado, fuso, cadastros, vivo.Value.CausasDaAprovacao));
    }

    // A recópia roda sempre, com a aprovação conferida ou não: a proteção que a aprovação recusa fica de fora das pendências que a
    // leem, e a recusa dela sai na frente das demais.
    private async Task<Result<CatalogoERecopia>> LerOCatalogoEARecopiaAsync(
        ProcessoSeletivo processo, OrigemDoCongelamento origem, CancellationToken cancellationToken)
    {
        CatalogoVivo catalogo = await catalogos.LerAsync(processo, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FieldError> daAprovacao = ConferenciaDaAprovacaoDaProtecao.Conferir(processo, catalogo.TiposVivos, catalogo.Fatos).Errors;
        Result<RecopiadoAoCongelar> recopiado = await recopia.RecopiarAsync(processo, catalogo, origem, cancellationToken).ConfigureAwait(false);

        return recopiado.IsFailure
            ? Result<CatalogoERecopia>.ValidationFailure([.. daAprovacao, .. recopiado.Errors])
            : Result<CatalogoERecopia>.Success(new CatalogoERecopia(catalogo, recopiado.Value!, daAprovacao));
    }

    // O tipo que a versão vigente já congelou mantém a classificação dela: mudar o catálogo depois não alcança o processo já
    // publicado, nem pela retificação. O catálogo e o calendário valem no dia em que a operação publica.
    private async Task<CadastrosDaConfiguracao> LerOsCadastrosDoDominioAsync(
        ProcessoSeletivo processo,
        AtoEDocumentoConferidos ato,
        IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados,
        CatalogoERecopia vivo,
        CancellationToken cancellationToken)
    {
        TiposDeAtoUnicosPorObjeto tipos = await tiposDeAto
            .ParaOperacaoAsync(processo, ato.TipoDoAto, ato.AtosDaLinhagem, ato.Publicacao.Dia, jaCongelados, cancellationToken)
            .ConfigureAwait(false);
        ContextoDeContagemDePrazos contexto = await contextos.LerAsync(cancellationToken).ConfigureAwait(false);
        CorretoresDasEtapas corretoresDasEtapas = await corretores.LerAsync(processo, cancellationToken).ConfigureAwait(false);

        return new CadastrosDaConfiguracao(contexto, vivo.Catalogo.FatosDeModalidade, tipos, corretoresDasEtapas, vivo.Recopiado.Protecao.Exigencias);
    }

    private static Result<ConfiguracaoConferida> Resultado(
        Result<ConfiguracaoAptaACongelar> daConfiguracao,
        OperacaoConferida daOperacao,
        ProcessoSeletivo processo,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem,
        LeiturasDoCongelamento lidos)
    {
        IReadOnlyList<FieldError> causas = [.. lidos.CausasDaAprovacao, .. daConfiguracao.Errors, .. daOperacao.Causas];

        return causas.Count > 0
            ? Result<ConfiguracaoConferida>.ValidationFailure(causas)
            : Result<ConfiguracaoConferida>.Success(
                new ConfiguracaoConferida(Entrada(processo, ato, origem, lidos, daOperacao), daConfiguracao.Value!, lidos.Cadastros));
    }

    private static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo, AtoEDocumentoConferidos ato, OrigemDoCongelamento origem, LeiturasDoCongelamento lidos, OperacaoConferida daOperacao) =>
        new(
            processo, daOperacao.Documentos.Dados!, ato.Documento.HashSha256!, lidos.Fuso.Id,
            Retificacao: origem.Retificacao,
            Conformidade: daOperacao.Documentos.Conformidade,
            MetadadosFatosCongelados: daOperacao.Fatos.Metadados,
            ValoresSelecionaveisCongelados: daOperacao.Fatos.Valores,
            AgregadosDosGrupos: daOperacao.Fatos.Agregados,
            ApresentacaoDosPressupostos: daOperacao.Fatos.Avaliavel!.ApresentacaoDosPressupostos,
            TiposDeAtoCongelados: daOperacao.Documentos.TiposDeAto,
            FatosDerivadosCongelados: lidos.Recopiado.Protecao.FatosDerivados);

    private sealed record CatalogoERecopia(CatalogoVivo Catalogo, RecopiadoAoCongelar Recopiado, IReadOnlyList<FieldError> CausasDaAprovacao);
}
