namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.ApoioDoVeredictoDeSubmissao;

/// <summary>
/// O resultado da submissão considera: a ausência de resposta a um item obrigatório, um
/// impedimento, um grupo fora do mínimo/máximo ou sem o candidato, e um termo obrigatório não
/// aceito (ou aceito com versão/hash diferente) recusam a submissão — nada disso é tolerado no
/// rascunho. Cada teste constrói a <see cref="AvaliacaoFormulario"/> diretamente, sem passar pelo
/// avaliador real: o que se prova aqui é SÓ a regra de aceitação/recusa sobre uma avaliação já
/// pronta, não a avaliação em si (coberta à parte, em <c>AvaliadorFormularioTests</c>).
/// </summary>
public sealed class VeredictoDeSubmissaoTests
{
    private static AvaliacaoItem ItemVisivelObrigatorio(string fato, IReadOnlyList<RestricaoValor>? restricoesVioladas = null, Ternario impedido = Ternario.Falso) =>
        new(fato, "DADOS", Ternario.Verdadeiro, Ternario.Verdadeiro, restricoesVioladas ?? [], impedido);

    [Fact(DisplayName = "Avaliação sem item, grupo ou termo pendente não recusa nada")]
    public void Avaliar_SemPendencia_NaoRecusaNada()
    {
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("Nanda")) },
            itens: [ItemVisivelObrigatorio("APELIDO")]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites, DiaDoEnvio);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Item visível e obrigatório sem resposta recusa a submissão")]
    public void Avaliar_ItemObrigatorioSemResposta_Recusa()
    {
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.Indeterminado() },
            itens: [ItemVisivelObrigatorio("APELIDO")]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites, DiaDoEnvio);

        recusas.Should().ContainSingle(r => r.Field == "APELIDO" && r.Error.Code == "Inscricao.ItemObrigatorioAusente");
    }

    [Fact(DisplayName = "Item opcional sem resposta, com a página concluída, não pesa")]
    public void Avaliar_ItemOpcionalNaoInformado_NaoRecusa()
    {
        AvaliacaoItem item = new("APELIDO", "DADOS", Ternario.Verdadeiro, Ternario.Falso, [], Ternario.Falso);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.NaoInformado() },
            itens: [item]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites, DiaDoEnvio);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Item oculto sem resposta não pesa, mesmo sendo obrigatório")]
    public void Avaliar_ItemOculto_NaoRecusa()
    {
        AvaliacaoItem item = new("APELIDO", "DADOS", Ternario.Falso, Ternario.Verdadeiro, [], Ternario.Falso);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.NaoAplicavel() },
            itens: [item]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites, DiaDoEnvio);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Resposta que viola restrição recusa, mesmo com resposta presente")]
    public void Avaliar_RestricaoViolada_Recusa()
    {
        AvaliacaoItem item = ItemVisivelObrigatorio("APELIDO", restricoesVioladas: [new TamanhoTexto(1, 5)]);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("Apelido Enorme")) },
            itens: [item]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites, DiaDoEnvio);

        recusas.Should().ContainSingle(r => r.Field == "APELIDO" && r.Error.Code == "Inscricao.RespostaInvalida");
    }

    [Fact(DisplayName = "Resposta que impede a inscrição recusa a submissão")]
    public void Avaliar_Impedimento_Recusa()
    {
        AvaliacaoItem item = ItemVisivelObrigatorio("CONDICAO_IMPEDITIVA", impedido: Ternario.Verdadeiro);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["CONDICAO_IMPEDITIVA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true)) },
            itens: [item]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites, DiaDoEnvio);

        recusas.Should().ContainSingle(r => r.Field == "CONDICAO_IMPEDITIVA" && r.Error.Code == "Inscricao.Impedimento");
    }

    [Fact(DisplayName = "Grupo visível fora do mínimo/máximo, sem o candidato, recusa cada violação")]
    public void Avaliar_GrupoContagemInvalidaESemCandidato_RecusaAsDuas()
    {
        AvaliacaoGrupo grupo = new(
            "COMPOSICAO_FAMILIAR", "DADOS", Ternario.Verdadeiro, Ternario.Falso, EstadoFato.Resolvido,
            ContagemValida: false, OcorrenciaDoCandidatoValida: false, Ocorrencias: []);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), grupos: [grupo]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites, DiaDoEnvio);

        recusas.Should().Contain(r => r.Field == "COMPOSICAO_FAMILIAR" && r.Error.Code == "Inscricao.GrupoContagemInvalida");
        recusas.Should().Contain(r => r.Field == "COMPOSICAO_FAMILIAR" && r.Error.Code == "Inscricao.GrupoCandidatoAusente");
    }

    [Fact(DisplayName = "Item obrigatório sem resposta dentro de uma ocorrência de grupo também recusa")]
    public void Avaliar_ItemObrigatorioSemRespostaDentroDoGrupo_Recusa()
    {
        AvaliacaoItem itemDoMembro = ItemVisivelObrigatorio("RG_NUMERO_MEMBRO");
        AvaliacaoOcorrencia ocorrencia = new(
            "membro-1", [itemDoMembro],
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["RG_NUMERO_MEMBRO"] = FatoResolvido.Indeterminado() },
            EstadoFato.Indeterminado);
        AvaliacaoGrupo grupo = new(
            "COMPOSICAO_FAMILIAR", "DADOS", Ternario.Verdadeiro, Ternario.Falso, EstadoFato.Indeterminado,
            ContagemValida: true, OcorrenciaDoCandidatoValida: true, Ocorrencias: [ocorrencia]);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), grupos: [grupo]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites, DiaDoEnvio);

        recusas.Should().ContainSingle(r => r.Field == "COMPOSICAO_FAMILIAR.membro-1.RG_NUMERO_MEMBRO" && r.Error.Code == "Inscricao.ItemObrigatorioAusente");
    }

    [Fact(DisplayName = "As recusas seguem a ordem dos itens, de cada grupo e suas ocorrências e dos termos")]
    public void Avaliar_ItensGruposEOcorrenciasETermos_RecusaNaOrdemDaAvaliacao()
    {
        AvaliacaoFormulario avaliacao = AvaliacaoComRecusasEmTodasAsPartes();
        IReadOnlyDictionary<TermoNoFormulario, TermoAdmitido> exigido = TermoExigido("DECLARACAO_VERACIDADE", Guid.NewGuid(), new string('a', 64));

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, exigido, SemAceites, DiaDoEnvio);

        recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("APELIDO", "Inscricao.ItemObrigatorioAusente"),
            ("GRUPO_1", "Inscricao.GrupoContagemInvalida"),
            ("GRUPO_1.membro-1.NOME_MEMBRO", "Inscricao.ItemObrigatorioAusente"),
            ("GRUPO_2", "Inscricao.GrupoContagemInvalida"),
            ("GRUPO_2.membro-2.NOME_MEMBRO", "Inscricao.ItemObrigatorioAusente"),
            (CodigoPrefixado, "Inscricao.TermoNaoAceito"));
    }

    [Fact(DisplayName = "Contagem abaixo do mínimo em grupo de obrigatoriedade indeterminada depende do Geo; as demais recusas não")]
    public void AvaliarDistinguindoOGeo_GrupoAbaixoDoMinimoComObrigatoriedadeIndeterminada_DistingueRecusaDependente()
    {
        AvaliacaoFormulario avaliacao = AvaliacaoComRecusasEmTodasAsPartes(Ternario.Indeterminado);
        IReadOnlyDictionary<TermoNoFormulario, TermoAdmitido> exigido = TermoExigido("DECLARACAO_VERACIDADE", Guid.NewGuid(), new string('a', 64));

        IReadOnlyList<RecusaDaSubmissao> recusas = VeredictoDeSubmissao.AvaliarDistinguindoOGeo(
            avaliacao, exigido, SemAceites, DiaDoEnvio, GeoComGrupoCondicional());

        recusas.Select(static r => (r.Recusa.Field, r.Recusa.Error.Code, r.DependeDoGeo)).Should().Equal(
            ("APELIDO", "Inscricao.ItemObrigatorioAusente", false),
            ("GRUPO_1", "Inscricao.GrupoContagemInvalida", true),
            ("GRUPO_1.membro-1.NOME_MEMBRO", "Inscricao.ItemObrigatorioAusente", false),
            ("GRUPO_2", "Inscricao.GrupoContagemInvalida", false),
            ("GRUPO_2.membro-2.NOME_MEMBRO", "Inscricao.ItemObrigatorioAusente", false),
            (CodigoPrefixado, "Inscricao.TermoNaoAceito", false));
    }

    private static AvaliacaoFormulario AvaliacaoComRecusasEmTodasAsPartes(Ternario obrigatoriedadeDoPrimeiroGrupo = Ternario.Falso) =>
        AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.Indeterminado() },
            itens: [ItemVisivelObrigatorio("APELIDO")],
            termos: [new AvaliacaoTermo(CodigoPrefixado, Ternario.Verdadeiro, Ternario.Verdadeiro)],
            grupos: [GrupoComRecusas("GRUPO_1", "membro-1", obrigatoriedadeDoPrimeiroGrupo), GrupoComRecusas("GRUPO_2", "membro-2", Ternario.Falso)]);

    private static AvaliacaoGrupo GrupoComRecusas(string codigo, string id, Ternario obrigatorio)
    {
        AvaliacaoOcorrencia ocorrencia = new(
            id, [ItemVisivelObrigatorio("NOME_MEMBRO")],
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["NOME_MEMBRO"] = FatoResolvido.Indeterminado() },
            EstadoFato.Indeterminado);
        return new(codigo, "DADOS", Ternario.Verdadeiro, obrigatorio, EstadoFato.Indeterminado,
            ContagemValida: false, OcorrenciaDoCandidatoValida: true, Ocorrencias: [ocorrencia]);
    }

    private static DependenciaDoGeo GeoComGrupoCondicional()
    {
        const string derivado = "RESIDE_NA_REGIAO";
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, CondicaoDnf.Criar(derivado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
        DefinicaoGrupo grupo = new("GRUPO_1", null, Obrigatoriedade.Quando(predicado), 2, null, [], false);
        DefinicaoFormulario definicao = new([new DefinicaoPagina("DADOS", null, [], [grupo])], [], []);
        return new(definicao, new HashSet<string>([derivado], StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> OfertaDeEstadoCivil =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["ESTADO_CIVIL"] = new HashSet<string>(["SOLTEIRO", "CASADO"], StringComparer.Ordinal),
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos =
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal);

    [Fact(DisplayName = "Resposta com código fora da oferta vigente recusa, mesmo que o avaliador a tenha descartado em silêncio")]
    public void RecusasDeOpcaoForaDaOferta_CodigoForaDaOferta_Recusa()
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement("DIVORCIADO"),
        };

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(respostas, SemGrupos, OfertaDeEstadoCivil);

        recusas.Should().ContainSingle(r => r.Field == "ESTADO_CIVIL" && r.Error.Code == "Inscricao.RespostaInvalida");
    }

    [Fact(DisplayName = "Resposta dentro da oferta vigente não recusa")]
    public void RecusasDeOpcaoForaDaOferta_CodigoDentroDaOferta_NaoRecusa()
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement("CASADO"),
        };

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(respostas, SemGrupos, OfertaDeEstadoCivil);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Resposta em branco não recusa, mesmo para fato ofertado")]
    public void RecusasDeOpcaoForaDaOferta_RespostaEmBranco_NaoRecusa()
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement((string?)null),
        };

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(respostas, SemGrupos, OfertaDeEstadoCivil);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fato sem oferta declarada não recusa, qualquer que seja a resposta")]
    public void RecusasDeOpcaoForaDaOferta_FatoSemOferta_NaoRecusa()
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["APELIDO"] = JsonSerializer.SerializeToElement("qualquer coisa"),
        };

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(respostas, SemGrupos, OfertaDeEstadoCivil);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Código fora da oferta dentro de uma ocorrência de grupo também recusa")]
    public void RecusasDeOpcaoForaDaOferta_CodigoForaDaOfertaDentroDoGrupo_Recusa()
    {
        OcorrenciaDeGrupoInscricao ocorrencia = new(
            "membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement("VIUVO") });
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [ocorrencia],
        };

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal), grupos, OfertaDeEstadoCivil);

        recusas.Should().ContainSingle(r => r.Field == "COMPOSICAO_FAMILIAR.membro-1.ESTADO_CIVIL" && r.Error.Code == "Inscricao.RespostaInvalida");
    }
}
