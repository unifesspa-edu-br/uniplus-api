namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O veredito de submissão (issue #1859) é NOVO: a ausência de resposta a um item obrigatório, um
/// impedimento, um grupo fora do mínimo/máximo ou sem o candidato, e um termo obrigatório não
/// aceito (ou aceito com versão/hash diferente) recusam a submissão — nada disso é tolerado no
/// rascunho. Cada teste constrói a <see cref="AvaliacaoFormulario"/> diretamente, sem passar pelo
/// avaliador real: o que se prova aqui é SÓ a regra de aceitação/recusa sobre uma avaliação já
/// pronta, não a avaliação em si (coberta à parte, em <c>AvaliadorFormularioTests</c>).
/// </summary>
public sealed class VeredictoDeSubmissaoTests
{
    private static readonly IReadOnlyList<TermoExigidoFormulario> SemTermos = [];
    private static readonly IReadOnlyList<AceiteDeTermo> SemAceites = [];

    private static AvaliacaoItem ItemVisivelObrigatorio(string fato, IReadOnlyList<RestricaoValor>? restricoesVioladas = null, Ternario impedido = Ternario.Falso) =>
        new(fato, "DADOS", Ternario.Verdadeiro, Ternario.Verdadeiro, restricoesVioladas ?? [], impedido);

    private static AvaliacaoFormulario AvaliacaoCom(
        IReadOnlyDictionary<string, FatoResolvido> fatos,
        IReadOnlyList<AvaliacaoItem>? itens = null,
        IReadOnlyList<AvaliacaoTermo>? termos = null,
        IReadOnlyList<AvaliacaoGrupo>? grupos = null) =>
        new(fatos, itens ?? [], termos ?? [], grupos ?? [], []);

    [Fact(DisplayName = "Avaliação sem item, grupo ou termo pendente não recusa nada")]
    public void Avaliar_SemPendencia_NaoRecusaNada()
    {
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("Nanda")) },
            itens: [ItemVisivelObrigatorio("APELIDO")]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Item visível e obrigatório sem resposta recusa a submissão")]
    public void Avaliar_ItemObrigatorioSemResposta_Recusa()
    {
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.Indeterminado() },
            itens: [ItemVisivelObrigatorio("APELIDO")]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().ContainSingle(r => r.Field == "APELIDO" && r.Error.Code == "Inscricao.ItemObrigatorioAusente");
    }

    [Fact(DisplayName = "Item opcional sem resposta, com a etapa concluída, não pesa")]
    public void Avaliar_ItemOpcionalNaoInformado_NaoRecusa()
    {
        AvaliacaoItem item = new("APELIDO", "DADOS", Ternario.Verdadeiro, Ternario.Falso, [], Ternario.Falso);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.NaoInformado() },
            itens: [item]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Item oculto sem resposta não pesa, mesmo sendo obrigatório")]
    public void Avaliar_ItemOculto_NaoRecusa()
    {
        AvaliacaoItem item = new("APELIDO", "DADOS", Ternario.Falso, Ternario.Verdadeiro, [], Ternario.Falso);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.NaoAplicavel() },
            itens: [item]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Resposta que viola restrição recusa, mesmo com resposta presente")]
    public void Avaliar_RestricaoViolada_Recusa()
    {
        AvaliacaoItem item = ItemVisivelObrigatorio("APELIDO", restricoesVioladas: [new TamanhoTexto(1, 5)]);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["APELIDO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("Apelido Enorme")) },
            itens: [item]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().ContainSingle(r => r.Field == "APELIDO" && r.Error.Code == "Inscricao.RespostaInvalida");
    }

    [Fact(DisplayName = "Resposta que impede a inscrição recusa a submissão")]
    public void Avaliar_Impedimento_Recusa()
    {
        AvaliacaoItem item = ItemVisivelObrigatorio("CONDICAO_IMPEDITIVA", impedido: Ternario.Verdadeiro);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["CONDICAO_IMPEDITIVA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true)) },
            itens: [item]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().ContainSingle(r => r.Field == "CONDICAO_IMPEDITIVA" && r.Error.Code == "Inscricao.Impedimento");
    }

    [Fact(DisplayName = "Grupo visível fora do mínimo/máximo, sem o candidato, recusa cada violação")]
    public void Avaliar_GrupoContagemInvalidaESemCandidato_RecusaAsDuas()
    {
        AvaliacaoGrupo grupo = new(
            "COMPOSICAO_FAMILIAR", "DADOS", Ternario.Verdadeiro, Ternario.Falso, EstadoFato.Resolvido,
            ContagemValida: false, OcorrenciaDoCandidatoValida: false, Ocorrencias: []);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), grupos: [grupo]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().Contain(r => r.Field == "COMPOSICAO_FAMILIAR" && r.Error.Code == "Inscricao.GrupoContagemInvalida");
        recusas.Should().Contain(r => r.Field == "COMPOSICAO_FAMILIAR" && r.Error.Code == "Inscricao.GrupoCandidatoAusente");
    }

    [Fact(DisplayName = "Duas ocorrências do mesmo grupo com o mesmo id recusam por id duplicado")]
    public void Avaliar_OcorrenciasComIdDuplicado_Recusa()
    {
        AvaliacaoOcorrencia ocorrencia1 = new("membro-1", [], new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), EstadoFato.Resolvido);
        AvaliacaoOcorrencia ocorrencia2 = new("membro-1", [], new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), EstadoFato.Resolvido);
        AvaliacaoGrupo grupo = new(
            "COMPOSICAO_FAMILIAR", "DADOS", Ternario.Verdadeiro, Ternario.Falso, EstadoFato.Resolvido,
            ContagemValida: true, OcorrenciaDoCandidatoValida: true, Ocorrencias: [ocorrencia1, ocorrencia2]);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), grupos: [grupo]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().ContainSingle(r => r.Field == "COMPOSICAO_FAMILIAR.membro-1" && r.Error.Code == "Inscricao.GrupoIdDuplicado");
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

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().ContainSingle(r => r.Field == "COMPOSICAO_FAMILIAR.membro-1.RG_NUMERO_MEMBRO" && r.Error.Code == "Inscricao.ItemObrigatorioAusente");
    }

    private static TermoExigidoFormulario NovoTermo(string codigo, Guid versaoId, string hash) =>
        TermoExigidoFormulario.Criar(
            codigo, 0,
            new VersaoTermoEscolhida(Guid.NewGuid(), versaoId, "Declaração de veracidade", "Declaro que é verdade.", "Lei 9.784/1999, art. 4º", "REGISTRO_DIGITAL_SEM_LOG_IP", hash),
            exibicao: null, Obrigatoriedade.Sempre).Value!;

    [Fact(DisplayName = "Termo obrigatório não aceito recusa a submissão")]
    public void Avaliar_TermoObrigatorioNaoAceito_Recusa()
    {
        AvaliacaoTermo termo = new("DECLARACAO_VERACIDADE", Ternario.Verdadeiro, Ternario.Verdadeiro);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), termos: [termo]);
        TermoExigidoFormulario exigido = NovoTermo("DECLARACAO_VERACIDADE", Guid.NewGuid(), new string('a', 64));

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, [exigido], SemAceites);

        recusas.Should().ContainSingle(r => r.Field == "DECLARACAO_VERACIDADE" && r.Error.Code == "Inscricao.TermoNaoAceito");
    }

    [Fact(DisplayName = "Termo aceito com hash diferente do da versão fixada recusa a submissão")]
    public void Avaliar_TermoAceitoComHashDiferente_Recusa()
    {
        AvaliacaoTermo termo = new("DECLARACAO_VERACIDADE", Ternario.Verdadeiro, Ternario.Verdadeiro);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), termos: [termo]);
        Guid versaoId = Guid.NewGuid();
        TermoExigidoFormulario exigido = NovoTermo("DECLARACAO_VERACIDADE", versaoId, new string('a', 64));
        AceiteDeTermo aceite = new("DECLARACAO_VERACIDADE", versaoId, new string('b', 64));

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, [exigido], [aceite]);

        recusas.Should().ContainSingle(r => r.Field == "DECLARACAO_VERACIDADE" && r.Error.Code == "Inscricao.TermoNaoAceito");
    }

    [Fact(DisplayName = "Termo aceito com a mesma versão e o mesmo hash da versão fixada não recusa")]
    public void Avaliar_TermoAceitoNaVersaoFixada_NaoRecusa()
    {
        AvaliacaoTermo termo = new("DECLARACAO_VERACIDADE", Ternario.Verdadeiro, Ternario.Verdadeiro);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), termos: [termo]);
        Guid versaoId = Guid.NewGuid();
        TermoExigidoFormulario exigido = NovoTermo("DECLARACAO_VERACIDADE", versaoId, new string('a', 64));
        AceiteDeTermo aceite = new("DECLARACAO_VERACIDADE", versaoId, new string('a', 64));

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, [exigido], [aceite]);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Termo opcional não aceito não recusa")]
    public void Avaliar_TermoOpcionalNaoAceito_NaoRecusa()
    {
        AvaliacaoTermo termo = new("TERMO_OPCIONAL", Ternario.Verdadeiro, Ternario.Falso);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), termos: [termo]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites);

        recusas.Should().BeEmpty();
    }
}
