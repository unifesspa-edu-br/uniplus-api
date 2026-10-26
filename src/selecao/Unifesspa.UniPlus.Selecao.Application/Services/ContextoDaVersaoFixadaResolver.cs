namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O contexto de avaliação da finalidade Inscrição na versão da inscrição (<see cref="VersaoDaInscricaoResolver"/>) —
/// nunca a vigente por relógio (UNI-REQ-0084): os termos exigidos dessa finalidade, a data de
/// referência dos fatos congelada na publicação, a oferta de valores de cada campo, o que a versão
/// admite como resposta, a configuração inteira que a avaliação da inscrição lê (ADR-0143) e o ato que
/// publicou a versão, de que é o endereço dos modelos de documento no acervo. Reusado por Salvar, por
/// Submeter e pela consulta do candidato, que avaliam a mesma versão do mesmo jeito.
/// </summary>
/// <remarks>
/// <see cref="Configuracao"/> vale para as ofertas que a inscrição escolheu: com a 1ª e a 2ª opção, a
/// união das distribuições das duas, de onde saem o formulário, os valores admitidos e os documentos.
/// As modalidades, porém, são por opção, porque cada oferta tem a distribuição dela:
/// <see cref="ConfiguracaoDaPrimeiraOpcao"/> e <see cref="ConfiguracaoDaSegundaOpcao"/> leem cada uma só a
/// distribuição da oferta da opção. A segunda é nula sem 2ª opção avaliável — não declarada, sem 1ª ou
/// em versão que admite uma só opção. <see cref="OfertasDeCurso"/> são as ofertas da versão fixada, com o
/// que as nomeia, para quem escolhe a opção não depender do certame vivo.
/// </remarks>
public sealed record ContextoDaVersaoFixada(
    IReadOnlyCollection<TermoExigidoFormulario> TermosExigidos,
    DateOnly? DataReferenciaFatos,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas,
    RespostasAdmitidas Admitidas,
    ConfiguracaoAvaliavel Configuracao,
    Guid AtoCriadorId,
    ConfiguracaoAvaliavel ConfiguracaoDaPrimeiraOpcao,
    ConfiguracaoAvaliavel? ConfiguracaoDaSegundaOpcao,
    IReadOnlyList<OfertaDeCursoCertameDto> OfertasDeCurso);

public static class ContextoDaVersaoFixadaResolver
{
    /// <param name="ofertaCursoId">
    /// A oferta de curso da 1ª opção que a inscrição escolhe — a gravada, ou a que o corpo da requisição
    /// grava. Nula, a avaliação lê a união de todas as distribuições (<see cref="ConfiguracaoAvaliavelDoProcesso"/>).
    /// </param>
    /// <param name="ofertaCursoSegundaOpcaoId">
    /// A oferta de curso da 2ª opção. Só conta com a 1ª escolhida e se a versão fixada admite duas
    /// opções: a recusa da 2ª fora disso é do agregado, não da avaliação.
    /// </param>
    /// <remarks>Lê <paramref name="versaoFormularioId"/> como está: o rascunho já tem de ter adotado a divulgada (<see cref="VersaoDaInscricaoResolver.AdotarAsync"/>).</remarks>
    public static async Task<Result<ContextoDaVersaoFixada>> ResolverAsync(
        Guid versaoFormularioId,
        Guid processoSeletivoId,
        Domain.Interfaces.IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        Guid? ofertaCursoId,
        Guid? ofertaCursoSegundaOpcaoId,
        CancellationToken cancellationToken)
    {
        Result<VersaoConfiguracao> resolvida = await VersaoFixadaDaInscricaoResolver
            .ResolverAsync(versaoFormularioId, processoSeletivoId, processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (resolvida.IsFailure)
        {
            return Result<ContextoDaVersaoFixada>.Failure(resolvida.Error!);
        }

        VersaoConfiguracao versao = resolvida.Value!;

        Result<EnvelopeReidratado> reidratado = Reidratar(versao, registroCodecs);
        if (reidratado.IsFailure)
        {
            return Result<ContextoDaVersaoFixada>.Failure(reidratado.Error!);
        }

        GrafoConfiguracao grafo = reidratado.Value!.Grafo;
        RespostasAdmitidas admitidas = Admitidas(grafo);

        // Oferta que o processo não conhece vale como escolha nenhuma, e a 2ª só conta quando o agregado a
        // admite: quem recusa o resto é ele, e a avaliação não mostra modalidades de uma opção que a
        // inscrição não pode ter.
        Guid? primeiraAvaliavel = ofertaCursoId is { } escolhida && admitidas.OfertasDeCurso.Contains(escolhida) ? escolhida : null;
        Guid? segundaAvaliavel = Inscricao.SegundaOpcaoAdmissivel(primeiraAvaliavel, ofertaCursoSegundaOpcaoId, admitidas)
            ? ofertaCursoSegundaOpcaoId
            : null;
        Result<ConfiguracaoDaOpcao> daPrimeira = ConfiguracaoDasOfertas(reidratado.Value!, primeiraAvaliavel is { } primeira ? [primeira] : []);
        if (daPrimeira.IsFailure)
        {
            return Result<ContextoDaVersaoFixada>.Failure(daPrimeira.Error!);
        }

        ConfiguracaoDaOpcao dasEscolhidas = daPrimeira.Value!;
        ConfiguracaoAvaliavel? daSegunda = null;
        if (segundaAvaliavel is { } segunda)
        {
            Result<ConfiguracaoDaOpcao> uniao = ConfiguracaoDasOfertas(reidratado.Value!, [primeiraAvaliavel!.Value, segunda]);
            Result<ConfiguracaoDaOpcao> soASegunda = ConfiguracaoDasOfertas(reidratado.Value!, [segunda]);
            if (uniao.IsFailure || soASegunda.IsFailure)
            {
                return Result<ContextoDaVersaoFixada>.Failure((uniao.IsFailure ? uniao : soASegunda).Error!);
            }

            dasEscolhidas = uniao.Value!;
            daSegunda = soASegunda.Value!.Configuracao;
        }

        Result<DateOnly?> dataReferencia = ProjecaoDoFormularioDaVersao.ResolverDataReferenciaFatos(versao);
        if (dataReferencia.IsFailure)
        {
            return Result<ContextoDaVersaoFixada>.Failure(dataReferencia.Error!);
        }

        IReadOnlyCollection<TermoExigidoFormulario> termos =
            [.. reidratado.Value!.Grafo.TermosExigidos.Where(static t => t.Finalidade == FinalidadeFormulario.Inscricao)];

        return Result<ContextoDaVersaoFixada>.Success(new ContextoDaVersaoFixada(
            termos, dataReferencia.Value, dasEscolhidas.Avaliavel.Ofertas, admitidas,
            dasEscolhidas.Configuracao, versao.AtoCriadorId,
            daPrimeira.Value!.Configuracao, daSegunda, OfertasDaVersao(grafo)));
    }

    /// <summary>O envelope congelado da versão, lido pelo codec que sabe ler a versão de esquema dele.</summary>
    public static Result<EnvelopeReidratado> Reidratar(VersaoConfiguracao versao, IRegistroCodecsEnvelope registroCodecs)
    {
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        return registroCodecs.SabeLer(versao.SchemaVersion)
            ? registroCodecs.Reidratar(versao)
            : Result<EnvelopeReidratado>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                $"A versão '{versao.SchemaVersion}' do envelope congelado não está entre as capacidades de leitura reconhecidas pelo codec vivo."));
    }

    private sealed record ConfiguracaoDaOpcao(DefinicaoAvaliavel Avaliavel, ConfiguracaoAvaliavel Configuracao);

    private static Result<ConfiguracaoDaOpcao> ConfiguracaoDasOfertas(EnvelopeReidratado envelope, IReadOnlyCollection<Guid> ofertasDeCurso)
    {
        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DoCongelado(envelope, ofertasDeCurso);
        return avaliavel.IsFailure
            ? Result<ConfiguracaoDaOpcao>.Failure(avaliavel.Error!)
            : Result<ConfiguracaoDaOpcao>.Success(new ConfiguracaoDaOpcao(
                avaliavel.Value!, ConfiguracaoAvaliavelDoProcesso.DoCongelado(envelope.Grafo, avaliavel.Value!, ofertasDeCurso)));
    }

    /// <summary>
    /// As ofertas de curso de cada versão pedida — a da inscrição, nunca a vigente. Cada processo tem
    /// as versões lidas uma vez e cada versão é reidratada uma vez. Versão que não se lê não derruba o
    /// resultado: fica de fora.
    /// </summary>
    public static async Task<Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>>> OfertasDasVersoesAsync(
        IEnumerable<(Guid ProcessoSeletivoId, Guid VersaoFormularioId)> versoesPedidas,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versoesPedidas);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>> ofertas = [];
        foreach (IGrouping<Guid, (Guid ProcessoSeletivoId, Guid VersaoFormularioId)> doProcesso in versoesPedidas.Distinct().GroupBy(static v => v.ProcessoSeletivoId))
        {
            IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
                .ObterVersoesAsync(doProcesso.Key, cancellationToken)
                .ConfigureAwait(false);
            foreach ((_, Guid versaoId) in doProcesso)
            {
                VersaoConfiguracao? versao = versoes.FirstOrDefault(v => v.Id == versaoId);
                if (versao is null)
                {
                    continue;
                }

                Result<EnvelopeReidratado> reidratado = Reidratar(versao, registroCodecs);
                if (reidratado.IsSuccess)
                {
                    ofertas[versaoId] = OfertasDaVersao(reidratado.Value!.Grafo);
                }
            }
        }

        return ofertas;
    }

    public static List<OfertaDeCursoCertameDto> OfertasDaVersao(GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(grafo);

        return
        [
            .. grafo.DistribuicaoVagas
                .Where(static d => d.OfertaCurso is not null)
                .OrderBy(static d => d.OfertaCursoOrigemId.ToString(), StringComparer.Ordinal)
                .Select(static d => new OfertaDeCursoCertameDto(
                    d.OfertaCursoOrigemId, d.OfertaCurso!.CursoNome, d.OfertaCurso.CursoGrau, d.OfertaCurso.UnidadeOfertanteNome, d.OfertaCurso.Turnos)),
        ];
    }

    public static RespostasAdmitidas Admitidas(GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(grafo);

        return new RespostasAdmitidas(
            DefinicoesDasRespostasDaInscricao(grafo),
            FatosDeNivelSuperiorDaInscricao(grafo),
            SubitensDosGruposDaInscricao(grafo),
            grafo.DistribuicaoVagas.Select(static d => d.OfertaCursoOrigemId).ToHashSet(),
            TermosDaInscricao(grafo),
            NumeroDeOpcoesDeCurso: grafo.NumeroDeOpcoesDeCurso ?? 1);
    }

    private static Dictionary<string, TermoAdmitido> TermosDaInscricao(GrafoConfiguracao grafo)
    {
        Dictionary<string, TermoAdmitido> termos = new(StringComparer.Ordinal);
        foreach (TermoExigidoFormulario termo in grafo.TermosExigidos.Where(static t => t.Finalidade == FinalidadeFormulario.Inscricao))
        {
            termos.TryAdd(termo.Codigo, new TermoAdmitido(termo.VersaoId, termo.HashVersao));
        }

        return termos;
    }

    private static Dictionary<string, DefinicaoDaResposta> DefinicoesDasRespostasDaInscricao(GrafoConfiguracao grafo)
    {
        Dictionary<string, DefinicaoDaResposta> definicoes = new(StringComparer.Ordinal);
        IEnumerable<FatoColetado> coletados = grafo.FatosColetados
            .Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao)
            .Concat(grafo.GruposColetados
                .Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
                .SelectMany(static g => g.Subitens));
        foreach (FatoColetado fato in coletados)
        {
            definicoes.TryAdd(fato.FatoCodigo, DefinicaoDaResposta.De(fato.TipoRenderizacao, fato.ClassificacaoProtecao, fato.Formato, fato.FatoCodigo));
        }

        return definicoes;
    }

    private static HashSet<string> FatosDeNivelSuperiorDaInscricao(GrafoConfiguracao grafo) =>
        grafo.FatosColetados
            .Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao)
            .Select(static f => f.FatoCodigo)
            .ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, IReadOnlySet<string>> SubitensDosGruposDaInscricao(GrafoConfiguracao grafo)
    {
        Dictionary<string, IReadOnlySet<string>> subitens = new(StringComparer.Ordinal);
        foreach (GrupoColetado grupo in grafo.GruposColetados.Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao))
        {
            subitens.TryAdd(grupo.Codigo, grupo.Subitens.Select(static s => s.FatoCodigo).ToHashSet(StringComparer.Ordinal));
        }

        return subitens;
    }
}
