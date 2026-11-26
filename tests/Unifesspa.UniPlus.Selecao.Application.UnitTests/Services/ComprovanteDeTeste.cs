namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Os insumos de um comprovante de teste: o envelope da versão, o comprovante emitido e o montador do titular.</summary>
internal static class ComprovanteDeTeste
{
    public static readonly DateTimeOffset Instante = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static JsonElement Json(object? valor) => JsonSerializer.SerializeToElement(valor);

    public static Dictionary<string, JsonElement> Respostas(params (string Fato, object? Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Fato, static r => Json(r.Valor), StringComparer.Ordinal);

    public static FatoColetado Texto(string codigo, string rotulo, int ordem = 1, string classificacao = "PESSOAL") => FatoColetadoDeTeste.Coletando(codigo, ordem)
        .ComRotulo(rotulo)
        .DeTexto("LIVRE")
        .ComClassificacao(classificacao)
        .Construir();

    public static EnvelopeReidratado Envelope(
        IReadOnlyList<FatoColetado>? itens = null,
        IReadOnlyList<GrupoColetado>? grupos = null,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas>? ofertas = null,
        int numeroDeOpcoes = 1,
        IReadOnlyDictionary<string, FatoDerivadoCongelado>? derivados = null,
        IdentificadorLegivel? identificador = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Comprovante");
        processo.DefinirItens([.. itens ?? []], grupos: grupos).IsSuccess.Should().BeTrue();
        ConfiguracaoPublicada configuracao = ConfiguracaoDoProcesso(processo, ofertas, numeroDeOpcoes, identificador);
        return new EnvelopeReidratado(
            configuracao, DadosEdital.Criar("001/2026", DivulgacaoDeTeste.InicioDoPeriodo, DivulgacaoDeTeste.FimDoPeriodo, Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null, new DadosDeCatalogoDaConfiguracaoPublicada(FatosDerivadosCongelados: derivados));
    }

    private static ConfiguracaoPublicada ConfiguracaoDoProcesso(ProcessoSeletivo processo, IReadOnlyList<ConfiguracaoDistribuicaoVagas>? ofertas,
        int numeroDeOpcoes, IdentificadorLegivel? identificador) => new(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processo.CronogramaFases]),
            new AvaliacaoDeclarada([.. processo.Etapas], [.. processo.CriteriosDesempate], processo.Classificacao!),
            new VagasDeclaradas([.. ofertas ?? [.. processo.DistribuicaoVagas]], numeroDeOpcoes, processo.Fada is not null,
                processo.Fada, null, processo.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processo.DocumentosExigidos], []),
            new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta([.. processo.FatosColetados], [.. processo.GruposColetados]), [.. processo.GruposColetados], [.. processo.Formularios], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), identificador);

    public static ComprovanteInscricao Emitir(
        Inscricao inscricao,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, JsonElement>? derivados = null,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>? grupos = null,
        IReadOnlyDictionary<string, JsonElement>? sensiveis = null,
        (Guid? Primeira, Guid? Segunda)? opcoes = null,
        IReadOnlyList<ModalidadeDeConcorrencia>? modalidades = null) =>
        ComprovanteInscricao.Emitir(
            inscricao.Id, "00000001", Instante, opcoes?.Primeira, respostas, derivados ?? new Dictionary<string, JsonElement>(),
            grupos ?? new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [], [],
            modalidades, sensiveis, opcoes?.Segunda);

    public static ComprovanteDaInscricaoDto Montar(
        Inscricao inscricao, ComprovanteInscricao comprovante, EnvelopeReidratado envelope, int numero = 1, int total = 1) =>
        ComprovanteDoTitular.Montar(new InsumosDoComprovante(
            inscricao, new ComprovanteNaPosicao(comprovante, numero, total), envelope, "PSS Medicina 2027", "Centro de Processos Seletivos", Prazo: null, Cancelamento: null));

    public static Inscricao UmaInscricao(Cpf? cpf = null) =>
        Inscricao.Iniciar(Guid.CreateVersion7(), cpf ?? PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer", Guid.CreateVersion7()).Value!;

    public static IEnumerable<ItemDoComprovanteDto> TodosOsItens(ComprovanteDaInscricaoDto dto) =>
        dto.Formulario.Secoes.SelectMany(static s => s.Itens);
}
