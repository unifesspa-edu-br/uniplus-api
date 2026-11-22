namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>As respostas, as versões e os elementos de formulário que os testes de submeter a inscrição montam.</summary>
internal static class FormularioDoEnvioDeTeste
{
    internal static readonly Guid ProcessoId = Guid.CreateVersion7();

    /// <summary>
    /// As respostas de TODO o conjunto básico do candidato (25 fatos, UNI-REQ-0144): toda inscrição
    /// coleta esses fatos na seção reservada, então o veredito de submissão só passa quando todos
    /// os obrigatórios estão preenchidos — não só o item que o teste declara além do básico.
    /// Escolhas que mantêm os condicionais fora de obrigatoriedade: nacionalidade NATO (sem
    /// documento de estrangeiro), órgão emissor da lista (sem o nome do outro órgão), não deseja nome
    /// social, tipo de endereço urbano (sem comunidade).
    /// </summary>
    internal static Dictionary<string, JsonElement> RespostasDoConjuntoBasico(Cpf cpf)
    {
        Dictionary<string, JsonElement> respostas = RespostasDeIdentificacao(cpf);
        foreach ((string codigo, JsonElement valor) in RespostasComplementares())
        {
            respostas.Add(codigo, valor);
        }

        return respostas;
    }

    private static Dictionary<string, JsonElement> RespostasDeIdentificacao(Cpf cpf) => new(StringComparer.Ordinal)
    {
        ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
        ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
        ["NACIONALIDADE"] = JsonSerializer.SerializeToElement("NATO"),
        ["CPF"] = JsonSerializer.SerializeToElement(cpf.Valor),
        ["RG_NUMERO"] = JsonSerializer.SerializeToElement("1234567"),
        ["RG_ORGAO_EMISSOR"] = JsonSerializer.SerializeToElement("SSP"),
        ["RG_UF"] = JsonSerializer.SerializeToElement("PA"),
        ["RG_DATA_EMISSAO"] = JsonSerializer.SerializeToElement("2010-01-01"),
        ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement("2000-01-01"),
        ["NATURALIDADE_UF"] = JsonSerializer.SerializeToElement("PA"),
        ["NATURALIDADE_MUNICIPIO"] = JsonSerializer.SerializeToElement("1501402"),
        ["NOME_MAE"] = JsonSerializer.SerializeToElement("Maria Drumond"),
        ["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement("SOLTEIRO"),
    };

    private static Dictionary<string, JsonElement> RespostasComplementares() => new(StringComparer.Ordinal)
    {
        ["SEXO"] = JsonSerializer.SerializeToElement("FEMININO"),
        ["SEXO_CIVIL"] = JsonSerializer.SerializeToElement("FEMININO"),
        ["IDENTIDADE_GENERO"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR"),
        ["ORIENTACAO_SEXUAL"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR"),
        ["COR_RACA"] = JsonSerializer.SerializeToElement("PARDA"),
        ["EMAIL"] = JsonSerializer.SerializeToElement("fernanda.drumond@example.com"),
        ["TELEFONE"] = JsonSerializer.SerializeToElement("91999999999"),
        ["TIPO_ENDERECO"] = JsonSerializer.SerializeToElement("URBANO"),
        ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new
        {
            cep = "66010000",
            cidade = new { codigoIbge = "1501402", uf = "PA" },
        }),
    };

    /// <summary>
    /// Um item obrigatório na seção do formulário, mais um termo obrigatório exigido na mesma
    /// finalidade; <paramref name="outrosItens"/> e <paramref name="grupos"/> entram na mesma seção, e
    /// <paramref name="ajustar"/> muda o processo antes de a versão o congelar.
    /// </summary>
    internal static (VersaoConfiguracao Versao, ConfiguracaoPublicada Configuracao, VersaoTermoEscolhida Termo) MontarVersaoComItemObrigatorioETermo(
        IReadOnlyList<FatoColetado>? outrosItens = null, IReadOnlyList<GrupoColetado>? grupos = null, Action<ProcessoSeletivo>? ajustar = null,
        Func<ProcessoSeletivo, (IReadOnlyList<DocumentoExigido> Documentos, IReadOnlyList<RegraDeComprovacao> Nos)>? exigencias = null)
    {
        VersaoTermoEscolhida versaoDoTermo = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Termo de veracidade", "Declaro que as informações são verdadeiras.",
            "Lei 9.784/1999", FormaAceite.RegistroDigitalSemLogIp, new string('b', 64));
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Submeter inscrição");
        Result itensDefinidos = processoBase.DefinirItens([ItemApelidoObrigatorio(), .. outrosItens ?? []], grupos: grupos);
        itensDefinidos.IsSuccess.Should().BeTrue(itensDefinidos.Error?.Message);
        processoBase.DefinirTermosDoFormulario(FinalidadeFormulario.Inscricao, [TermoDeVeracidade(versaoDoTermo)]).IsSuccess.Should().BeTrue(
            "o termo não cita fato nenhum, então não tem citação pendente de garantir");
        ajustar?.Invoke(processoBase);

        return (VersaoComDataDeReferenciaFixa(), ConfiguracaoDoProcesso(processoBase, exigencias), versaoDoTermo);
    }

    internal static FatoColetado ItemApelidoObrigatorio() => FatoColetadoDeTeste.Coletando("APELIDO", 0)
        .ComRotulo("Apelido")
        .DeTexto("LIVRE")
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComRestricoes()
        .Construir();

    internal static TermoExigidoFormulario TermoDeVeracidade(VersaoTermoEscolhida versaoDoTermo) => TermoExigidoFormulario.Criar(
        "TERMO_VERACIDADE", 0, versaoDoTermo, exibicao: null, Obrigatoriedade.Sempre, FinalidadeFormulario.Inscricao).Value!;

    internal static ConfiguracaoPublicada ConfiguracaoDoProcesso(
        ProcessoSeletivo processoBase, Func<ProcessoSeletivo, (IReadOnlyList<DocumentoExigido> Documentos, IReadOnlyList<RegraDeComprovacao> Nos)>? exigencias)
    {
        (IReadOnlyList<DocumentoExigido> documentos, IReadOnlyList<RegraDeComprovacao> nos) =
            exigencias?.Invoke(processoBase) ?? ([.. processoBase.DocumentosExigidos], []);

        return new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processoBase.CronogramaFases]),
            new AvaliacaoDeclarada([.. processoBase.Etapas], [.. processoBase.CriteriosDesempate], processoBase.Classificacao!),
            new VagasDeclaradas([.. processoBase.DistribuicaoVagas], 1, processoBase.BonusRegional is not null,
                processoBase.BonusRegional, null, processoBase.AtendimentoEspecializado!),
            new ExigenciasDeclaradas(documentos, nos),
            new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta([.. processoBase.FatosColetados], [.. processoBase.GruposColetados]), [.. processoBase.GruposColetados], [.. processoBase.Formularios], [.. processoBase.TermosExigidos],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);
    }

    // documentosExigidos.dataReferenciaFatos congelada: o conjunto básico traz DATA_NASCIMENTO,
    // que FAIXA_ETARIA apura contra esta data, nunca a de hoje.
    internal static VersaoConfiguracao VersaoComDataDeReferenciaFixa() => VersaoConfiguracao.Abrir(
        ProcessoId,
        new DocumentoCongelado(Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":"2026-12-10"}}"""), "0.0.22", "canonical-json/sha256@v1", new string('a', 64)),
        Guid.CreateVersion7(), "user-conta-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    internal static InscricaoRequest RequestComApelidoETermoAceito(
        Cpf cpf, VersaoTermoEscolhida termo, ConfiguracaoPublicada configuracao, string apelido = "Nanda")
    {
        Dictionary<string, JsonElement> respostas = RespostasDoConjuntoBasico(cpf);
        respostas["APELIDO"] = JsonSerializer.SerializeToElement(apelido);
        return new(OfertaDo(configuracao), respostas, null, [new AceiteDeTermoInput("TERMO_VERACIDADE", termo.VersaoId, termo.Hash)]);
    }

    internal static Guid OfertaDo(ConfiguracaoPublicada configuracao) => configuracao.Vagas.DistribuicaoVagas[0].OfertaCursoOrigemId;

    /// <summary>Um booleano que, respondido "não", esconde o que depende dele.</summary>
    internal static FatoColetado Pergunta(string fato, int ordem) => FatoColetadoDeTeste.Coletando(fato, ordem)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComClassificacao("SENSIVEL")
        .Construir();

    internal static GrupoColetado GrupoMunicipal() => GrupoColetadoDeTeste.Agrupando("LOCAIS", 1)
        .ComRotulo("Locais")
        .ComOcorrencias(0, null)
        .ComCampos(FatoColetadoDeTeste.Coletando("LOCAL_MUNICIPIO", 0)
            .ComRotulo("Município")
            .DoTipo(TipoRenderizacao.Municipio)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComRestricoes(new MunicipiosDaUf("NATURALIDADE_UF"))
            .Construir())
        .DaFinalidade(FinalidadeFormulario.Inscricao)
        .Construir();

    internal static Dictionary<string, JsonElement> Texto(params (string Fato, string Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Fato, static r => JsonSerializer.SerializeToElement(r.Valor), StringComparer.Ordinal);
}
