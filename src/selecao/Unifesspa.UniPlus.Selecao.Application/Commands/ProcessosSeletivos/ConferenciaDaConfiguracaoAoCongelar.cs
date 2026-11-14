namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>
/// A configuração do processo conferida e pronta para ser canonicalizada, com o que a montagem do pedido precisa dela
/// fora da entrada do canonicalizador.
/// </summary>
internal sealed record ConfiguracaoConferida(
    EntradaCanonicalizacao Entrada, FatosDeModalidade FatosDeModalidade, TiposDeAtoUnicosPorObjeto TiposDeAtoUnicos);

/// <summary>
/// Os portões que a publicação, a retificação de ato único e o fechamento da sessão cruzam antes de canonicalizar,
/// na ordem da recusa (ADR-0109 D5, ADR-0125): a lista de <c>Portoes</c> é essa ordem. A recusa do primeiro que falha
/// sai como está, em lote ou isolada, e nada é canonicalizado nem gravado. O que difere entre as operações vem da
/// <see cref="OrigemDoCongelamento"/>.
/// </summary>
public sealed class ConferenciaDaConfiguracaoAoCongelar
{
    private readonly ConferenciaDoCatalogoEDaProtecao _catalogos;
    private readonly RecopiaAoCongelar _recopia;
    private readonly IResolvedorFusoInstitucional _resolvedorFuso;
    private readonly ResolucaoDosTiposDeAtoUnicos _tiposDeAto;
    private readonly ConferenciaDosAnexosDoEdital _anexos;
    private readonly ConferenciaDeConformidadeLegal _conformidadeLegal;
    private readonly ConferenciaDosMotivosAoCongelar _motivos;

    public ConferenciaDaConfiguracaoAoCongelar(
        ConferenciaDoCatalogoEDaProtecao catalogos,
        RecopiaAoCongelar recopia,
        IResolvedorFusoInstitucional resolvedorFuso,
        ResolucaoDosTiposDeAtoUnicos tiposDeAto,
        ConferenciaDosAnexosDoEdital anexos,
        ConferenciaDeConformidadeLegal conformidadeLegal,
        ConferenciaDosMotivosAoCongelar motivos)
    {
        _catalogos = catalogos;
        _recopia = recopia;
        _resolvedorFuso = resolvedorFuso;
        _tiposDeAto = tiposDeAto;
        _anexos = anexos;
        _conformidadeLegal = conformidadeLegal;
        _motivos = motivos;
    }

    internal async Task<Result<ConfiguracaoConferida>> ConferirAsync(
        ProcessoSeletivo processo,
        EditalDeclarado edital,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem,
        CancellationToken cancellationToken)
    {
        ConfiguracaoEmConferencia emConferencia = new(this, processo, edital, ato, origem);

        return await emConferencia.ConferirAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A configuração enquanto os portões a conferem: cada um deixa o que leu ou resolveu para os seguintes, e por
    /// isso a conferência vive só pela duração de uma operação.
    /// </summary>
    private sealed class ConfiguracaoEmConferencia(
        ConferenciaDaConfiguracaoAoCongelar servicos,
        ProcessoSeletivo processo,
        EditalDeclarado edital,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem)
    {
        private CatalogoVivo? _catalogo;
        private RecopiadoAoCongelar? _recopiado;
        private TimeZoneInfo? _fuso;
        private TiposDeAtoUnicosPorObjeto? _tiposDeAtoUnicos;
        private IReadOnlyDictionary<string, TipoDeAtoCongelado>? _tiposDeAtoCongelados;
        private DadosEdital? _dados;
        private ResultadoConformidade? _conformidade;
        private IReadOnlyDictionary<string, MetadadoFatoCongelado>? _metadados;
        private IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? _valores;
        private IReadOnlyList<DefinicaoAgregado>? _agregados;
        private DefinicaoAvaliavel? _avaliavel;

        public async Task<Result<ConfiguracaoConferida>> ConferirAsync(CancellationToken cancellationToken)
        {
            foreach (Func<CancellationToken, Task<Result>> portao in Portoes())
            {
                Result recusa = await portao(cancellationToken).ConfigureAwait(false);
                if (recusa.IsFailure)
                {
                    return Result<ConfiguracaoConferida>.ValidationFailure(recusa.Errors);
                }
            }

            return Result<ConfiguracaoConferida>.Success(Conferida());
        }

        private Func<CancellationToken, Task<Result>>[] Portoes() =>
        [
            ConferirOCatalogoAsync,
            RecopiarAsync,
            ConferirOChecklistEACascata,
            ConferirOFusoEOCronograma,
            ConferirOsTiposDeAtoUnicosAsync,
            ConferirOsTiposDeAtoCongelados,
            ConferirOEditalAsync,
            ConferirAConformidadeLegalAsync,
            ConferirAProntidaoEOsMetadados,
            ConferirOsValoresSelecionaveis,
            ConferirOsEixosDosFatosColetados,
            ConferirADefinicaoEOsMotivosAsync,
        ];

        private async Task<Result> ConferirOCatalogoAsync(CancellationToken cancellationToken)
        {
            Result<CatalogoVivo> catalogo = await servicos._catalogos.ConferirAsync(processo, cancellationToken).ConfigureAwait(false);
            _catalogo = catalogo.Value;

            return catalogo.IsFailure ? Result.ValidationFailure(catalogo.Errors) : Result.Success();
        }

        private async Task<Result> RecopiarAsync(CancellationToken cancellationToken)
        {
            Result<RecopiadoAoCongelar> recopiado = await servicos._recopia
                .RecopiarAsync(processo, _catalogo!, origem, cancellationToken)
                .ConfigureAwait(false);
            _recopiado = recopiado.Value;

            return recopiado.IsFailure ? Result.ValidationFailure(recopiado.Errors) : Result.Success();
        }

        // O gate precede a canonicalização (ADR-0109 D5): um processo não conforme não chega a ser projetado. Sem isso,
        // a canonicalização de uma dimensão obrigatória ausente falharia alto (D8) em vez de devolver o DomainError que
        // o contrato HTTP promete. A raiz reavalia — este é o guarda antecipado, não a autoridade. A cascata de
        // remanejamento de cotas (UNI-REQ-0056) é cross-dimensão e fica fora do agregador genérico do checklist.
        private Task<Result> ConferirOChecklistEACascata(CancellationToken _)
        {
            EstadoDaConfiguracao estado = EstadoDaConfiguracao.De(processo);
            DomainError? pendencia = ChecklistDeConformidade.PendenciaDeConformidade(estado) ?? CoberturaDaCascata.PendenciaDaCascata(estado);

            return Task.FromResult(pendencia is null ? Result.Success() : Result.Failure(pendencia));
        }

        // O fuso é antecipado: a conferência legal precisa dele para derivar o dia civil do início da inscrição. A falha
        // dele é defeito de instalação (500), não gate. A janela de isenção conta cinco dias corridos, e o quinto dia só
        // se completa no último instante do dia no fuso institucional, por isso o cronograma vem depois.
        private Task<Result> ConferirOFusoEOCronograma(CancellationToken _)
        {
            Result<TimeZoneInfo> fuso = servicos._resolvedorFuso.Resolver();
            if (fuso.IsFailure)
            {
                return Task.FromResult(Result.Failure(fuso.Error!));
            }

            _fuso = fuso.Value;
            DomainError? pendencia = PendenciasDoCronograma.PendenciaDoCronograma(EstadoDaConfiguracao.De(processo), _fuso!);

            return Task.FromResult(pendencia is null ? Result.Success() : Result.Failure(pendencia));
        }

        // O tipo que a versão vigente já congelou mantém a classificação dela: mudar o catálogo depois não alcança o
        // processo já publicado, nem pela retificação.
        private async Task<Result> ConferirOsTiposDeAtoUnicosAsync(CancellationToken cancellationToken)
        {
            Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> jaCongelados = origem.TiposJaCongelados();
            if (jaCongelados.IsFailure)
            {
                return Result.Failure(jaCongelados.Error!);
            }

            Result<TiposDeAtoUnicosPorObjeto> unicos = await servicos._tiposDeAto
                .ConferirAsync(processo, ato.TipoDoAto, ato.AtosDaLinhagem, ato.Publicacao.Dia, jaCongelados.Value!, cancellationToken)
                .ConfigureAwait(false);
            _tiposDeAtoUnicos = unicos.Value;

            return unicos.IsFailure ? Result.Failure(unicos.Error!) : Result.Success();
        }

        private Task<Result> ConferirOsTiposDeAtoCongelados(CancellationToken _)
        {
            Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> congelados = CongelamentoDosTiposDeAto.Resolver(processo, _tiposDeAtoUnicos!);
            _tiposDeAtoCongelados = congelados.Value;

            return Task.FromResult(congelados.IsFailure ? Result.Failure(congelados.Error!) : Result.Success());
        }

        private async Task<Result> ConferirOEditalAsync(CancellationToken cancellationToken)
        {
            Result<IReadOnlyList<AnexoDoEdital>> anexos = await servicos._anexos
                .ConferirAsync(processo.Id, edital.Anexos, origem.AnexosVigentes(herdaOsDaVersao: edital.Anexos is null), cancellationToken)
                .ConfigureAwait(false);
            if (anexos.IsFailure)
            {
                return Result.Failure(anexos.Error!);
            }

            Result<DadosEdital> dados = ResolucaoDoPeriodoDeInscricao.Resolver(
                processo, edital.Numero, edital.PeriodoInscricaoInicio, edital.PeriodoInscricaoFim, edital.DocumentoEditalId, anexos.Value!);
            _dados = dados.Value;

            return dados.IsFailure ? Result.Failure(dados.Error!) : Result.Success();
        }

        private async Task<Result> ConferirAConformidadeLegalAsync(CancellationToken cancellationToken)
        {
            Result<ResultadoConformidade> conformidade = await servicos._conformidadeLegal
                .AvaliarAsync(processo, _dados!.DiaDeReferenciaLegal(_fuso!), _catalogo!.FatosDeModalidade, cancellationToken)
                .ConfigureAwait(false);
            _conformidade = conformidade.Value;

            return conformidade.IsFailure ? Result.Failure(conformidade.Error!) : Result.Success();
        }

        // Terceira dimensão de conformidade (ADR-0109 D5): documentos exigidos, coerência da consequência de
        // indeferimento e referência temporal de fatos. A canonicalização resolve dataReferenciaFatos internamente e
        // LANÇA quando a política não resolve — sem este guard antes dela, um processo inválido vira exceção não
        // tratada em vez do DomainError que o contrato HTTP promete.
        private Task<Result> ConferirAProntidaoEOsMetadados(CancellationToken _)
        {
            DomainError? pendencia = ProntidaoParaCongelar.PendenciaPreCanonicalizacao(
                EstadoDaConfiguracao.De(processo), _catalogo!.FatosDeModalidade, _recopiado!.Protecao.Exigencias);
            if (pendencia is not null)
            {
                return Task.FromResult(Result.Failure(pendencia));
            }

            Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> metadados = ConferenciaDosFatosAoCongelar.Conferir(processo, _catalogo.PorCodigo);
            _metadados = metadados.Value;

            return Task.FromResult(metadados.IsFailure ? Result.Failure(metadados.Error!) : Result.Success());
        }

        // UNI-REQ-0072: os valores que o candidato pode escolher para cada fato de seleção coletado.
        private Task<Result> ConferirOsValoresSelecionaveis(CancellationToken _)
        {
            Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> valores =
                ResolvedorValoresSelecionaveisCongelados.Resolver(processo, _catalogo!.PorCodigo);
            _valores = valores.Value;

            return Task.FromResult(valores.IsFailure ? Result.Failure(valores.Error!) : Result.Success());
        }

        // O metadado dos fatos dos gatilhos e a operação dos agregados vêm do catálogo na hora de congelar: o fato já
        // coletado por versão publicada não os troca (UNI-REQ-0144).
        private Task<Result> ConferirOsEixosDosFatosColetados(CancellationToken _)
        {
            _agregados = DefinicaoAvaliavelDoProcesso.AgregadosDosGrupos(processo.GruposColetados, _catalogo!.PorCodigo);
            DomainError? eixoTrocado = EixoTrocadoNasVersoesPublicadas() ?? origem.ItemComEixoTrocado(processo);

            return Task.FromResult(eixoTrocado is null ? Result.Success() : Result.Failure(eixoTrocado));
        }

        private DomainError? EixoTrocadoNasVersoesPublicadas() =>
            _recopiado!.Publicadas is { } publicadas
                ? ConferenciaDosEixosDosFatosColetados.Conferir(publicadas, _metadados, _agregados!)
                : null;

        // Os fatos que as regras citam e nenhum formulário pergunta saem com o rótulo e o tipo do catálogo, congelados
        // na versão; o fato citado sem cadastro recusa aqui, antes de canonicalizar. Os motivos vêm por último.
        private async Task<Result> ConferirADefinicaoEOsMotivosAsync(CancellationToken cancellationToken)
        {
            Result<DefinicaoAvaliavel> avaliavel = await servicos._motivos
                .UltimosPortoesAsync(processo, _catalogo!.Fatos, cancellationToken)
                .ConfigureAwait(false);
            _avaliavel = avaliavel.Value;

            return avaliavel.IsFailure ? Result.ValidationFailure(avaliavel.Errors) : Result.Success();
        }

        private ConfiguracaoConferida Conferida() =>
            new(
                new EntradaCanonicalizacao(
                    processo, _dados!, ato.Documento.HashSha256!, _fuso!.Id,
                    Retificacao: origem.Retificacao,
                    Conformidade: _conformidade,
                    MetadadosFatosCongelados: _metadados,
                    ValoresSelecionaveisCongelados: _valores,
                    AgregadosDosGrupos: _agregados,
                    ApresentacaoDosPressupostos: _avaliavel!.ApresentacaoDosPressupostos,
                    TiposDeAtoCongelados: _tiposDeAtoCongelados,
                    FatosDerivadosCongelados: _recopiado!.Protecao.FatosDerivados),
                _catalogo!.FatosDeModalidade,
                _tiposDeAtoUnicos!);
    }
}
