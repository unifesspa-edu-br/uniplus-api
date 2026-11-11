namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Invariantes do cadastro único de motivos de decisão (UNI-REQ-0120 a UNI-REQ-0122,
/// UNI-REQ-0283): tipo fechado, subclassificação só na isenção, um sentido por motivo e só a
/// descrição editável.
/// </summary>
public sealed class MotivoDeDecisaoTests
{
    private const string DescricaoValida = "Renda familiar per capita acima do limite legal.";

    [Fact(DisplayName = "Motivo da isenção nasce ativo, com o fundamento como subclassificação")]
    public void Criar_Isencao_NasceAtivo()
    {
        MotivoDeDecisao motivo = CriarValido();

        motivo.Tipo.Should().Be(TipoMotivoDeDecisao.IsencaoDaTaxa);
        motivo.Codigo.Valor.Should().Be("RENDA_ACIMA_DO_LIMITE");
        motivo.Descricao.Should().Be(DescricaoValida);
        motivo.Subclassificacao.Should().Be(FundamentoIsencaoCodigo.CadastroUnico);
        motivo.Sentido.Should().Be(SentidoDoMotivo.Desfavoravel);
        motivo.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Motivo da homologação das inscrições nasce sem subclassificação")]
    public void Criar_Homologacao_SemSubclassificacao()
    {
        Result<MotivoDeDecisao> resultado = Criar(
            TipoMotivoDeDecisao.HomologacaoDasInscricoes, "SEM_RESULTADO_INEP", null, SentidoDoMotivo.Desfavoravel,
            "Sem resultado no retorno do INEP para a edição indicada");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Subclassificacao.Should().BeNull();
    }

    [Theory(DisplayName = "Os tipos sem subclassificação recusam a que vier declarada")]
    [InlineData(TipoMotivoDeDecisao.HomologacaoDasInscricoes)]
    [InlineData(TipoMotivoDeDecisao.AnaliseDocumental)]
    [InlineData(TipoMotivoDeDecisao.Heteroidentificacao)]
    [InlineData(TipoMotivoDeDecisao.AvaliacaoBiopsicossocial)]
    [InlineData(TipoMotivoDeDecisao.Classificacao)]
    public void Criar_TipoSemSubclassificacao_RecusaADeclarada(TipoMotivoDeDecisao tipo)
    {
        Result<MotivoDeDecisao> resultado = Criar(tipo, "DOCUMENTO_ILEGIVEL", FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "subclassificacao" && e.Error.Code == MotivoDeDecisaoErrorCodes.SubclassificacaoNaoAdmitida);
    }

    [Fact(DisplayName = "A isenção exige o fundamento como subclassificação")]
    public void Criar_IsencaoSemFundamento_Recusa()
    {
        Result<MotivoDeDecisao> resultado = Criar(TipoMotivoDeDecisao.IsencaoDaTaxa, "RENDA_ACIMA_DO_LIMITE", "  ", SentidoDoMotivo.Desfavoravel);

        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.SubclassificacaoObrigatoria);
    }

    [Fact(DisplayName = "A isenção recusa fundamento fora do vocabulário")]
    public void Criar_IsencaoComFundamentoDesconhecido_Recusa()
    {
        Result<MotivoDeDecisao> resultado = Criar(TipoMotivoDeDecisao.IsencaoDaTaxa, "RENDA_ACIMA_DO_LIMITE", "BOLSA_FAMILIA", SentidoDoMotivo.Desfavoravel);

        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.SubclassificacaoInvalida);
    }

    [Fact(DisplayName = "O fundamento declarado com espaços é normalizado")]
    public void Criar_IsencaoComFundamentoComEspacos_Normaliza()
    {
        Result<MotivoDeDecisao> resultado = Criar(TipoMotivoDeDecisao.IsencaoDaTaxa, "RENDA_ACIMA_DO_LIMITE", " DOACAO_MEDULA_OSSEA ", SentidoDoMotivo.Favoravel);

        resultado.Value!.Subclassificacao.Should().Be(FundamentoIsencaoCodigo.DoacaoMedulaOssea);
    }

    [Fact(DisplayName = "Motivo sem sentido é recusado")]
    public void Criar_SemSentido_Recusa()
    {
        Result<MotivoDeDecisao> resultado = Criar(TipoMotivoDeDecisao.IsencaoDaTaxa, "RENDA_ACIMA_DO_LIMITE", FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Nenhum);

        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.SentidoObrigatorio);
    }

    [Fact(DisplayName = "Sem tipo, a subclassificação não é conferida e as demais violações se acumulam")]
    public void Criar_PayloadInteiroInvalido_AcumulaViolacoes()
    {
        Result<MotivoDeDecisao> resultado = MotivoDeDecisao.Criar(new CamposDoMotivo(
            TipoMotivoDeDecisao.Nenhum, "código minúsculo", "  ", "QUALQUER", SentidoDoMotivo.Nenhum));

        resultado.Errors.Select(erro => erro.Error.Code).Should().BeEquivalentTo(
            [
                MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido,
                MotivoDeDecisaoErrorCodes.DescricaoObrigatoria,
                MotivoDeDecisaoErrorCodes.TipoObrigatorio,
                MotivoDeDecisaoErrorCodes.SentidoObrigatorio,
            ],
            "o errors[] do contrato público precisa de todas as regras violadas no mesmo lote");
    }

    [Fact(DisplayName = "Código ausente é recusado com o campo nomeado")]
    public void Criar_SemCodigo_Recusa()
    {
        Result<MotivoDeDecisao> resultado = Criar(TipoMotivoDeDecisao.Classificacao, null, null, SentidoDoMotivo.Desfavoravel);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "codigo" && e.Error.Code == MotivoDeDecisaoErrorCodes.CodigoObrigatorio);
    }

    [Theory(DisplayName = "Descrição fora do tamanho é recusada")]
    [InlineData("ab")]
    [InlineData(null)]
    public void Criar_DescricaoForaDoTamanho_Recusa(string? semTamanho)
    {
        string descricao = semTamanho ?? new string('a', ValidacaoDoMotivo.DescricaoMaxLength + 1);

        Result<MotivoDeDecisao> resultado = Criar(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", null, SentidoDoMotivo.Desfavoravel, descricao);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Descrição com caractere nulo é recusada pelo agregado")]
    public void Criar_DescricaoComCaractereNulo_Recusa()
    {
        // O caractere nulo não é espaço em branco e cabe no tamanho, mas a coluna textual do banco
        // não o armazena: sem a recusa o payload inválido só falharia ao gravar.
        Result<MotivoDeDecisao> resultado = Criar(
            TipoMotivoDeDecisao.IsencaoDaTaxa, "RENDA_ACIMA_DO_LIMITE", FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "Renda\0acima do limite.");

        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.DescricaoCaractereInvalido);
    }

    [Fact(DisplayName = "A edição alcança só a descrição: tipo, código, subclassificação e sentido não mudam")]
    public void Atualizar_TrocaDescricao_PreservaOsDemaisAtributos()
    {
        MotivoDeDecisao motivo = CriarValido();

        Result resultado = motivo.Atualizar("Renda per capita apurada acima do teto da Lei 12.799/2013.");

        resultado.IsSuccess.Should().BeTrue();
        motivo.Descricao.Should().Be("Renda per capita apurada acima do teto da Lei 12.799/2013.");
        motivo.Tipo.Should().Be(TipoMotivoDeDecisao.IsencaoDaTaxa);
        motivo.Codigo.Valor.Should().Be("RENDA_ACIMA_DO_LIMITE");
        motivo.Subclassificacao.Should().Be(FundamentoIsencaoCodigo.CadastroUnico);
        motivo.Sentido.Should().Be(SentidoDoMotivo.Desfavoravel);
    }

    [Fact(DisplayName = "Os atributos imutáveis não têm setter público")]
    public void AtributosImutaveis_SemSetterPublico()
    {
        string[] imutaveis = [nameof(MotivoDeDecisao.Tipo), nameof(MotivoDeDecisao.Codigo), nameof(MotivoDeDecisao.Subclassificacao), nameof(MotivoDeDecisao.Sentido)];

        typeof(MotivoDeDecisao).GetProperties()
            .Where(p => imutaveis.Contains(p.Name))
            .Should().OnlyContain(p => p.SetMethod == null || !p.SetMethod.IsPublic);
    }

    [Fact(DisplayName = "A edição não muda a situação do motivo")]
    public void Atualizar_MotivoDesativado_ContinuaDesativado()
    {
        MotivoDeDecisao motivo = CriarValido();
        motivo.Desativar();

        motivo.Atualizar("Nova redação.");

        motivo.Ativo.Should().BeFalse("corrigir o texto de um motivo retirado do cadastro não o devolve às publicações");
    }

    [Fact(DisplayName = "Descrição em branco é recusada na edição sem mexer na atual")]
    public void Atualizar_DescricaoEmBranco_Recusa()
    {
        MotivoDeDecisao motivo = CriarValido();

        Result resultado = motivo.Atualizar("   ");

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.DescricaoObrigatoria);
        motivo.Descricao.Should().Be(DescricaoValida);
    }

    [Fact(DisplayName = "Desativar preserva o registro e apenas o retira das novas publicações")]
    public void Desativar_MotivoAtivo_PreservaORegistro()
    {
        MotivoDeDecisao motivo = CriarValido();

        Result resultado = motivo.Desativar();

        resultado.IsSuccess.Should().BeTrue();
        motivo.Ativo.Should().BeFalse();
        motivo.Codigo.Valor.Should().Be("RENDA_ACIMA_DO_LIMITE");
    }

    [Fact(DisplayName = "Desativar motivo já inativo é recusado")]
    public void Desativar_MotivoInativo_Recusa()
    {
        MotivoDeDecisao motivo = CriarValido();
        motivo.Desativar();

        motivo.Desativar().Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JaInativo);
    }

    [Fact(DisplayName = "Reativar devolve o motivo às novas publicações")]
    public void Reativar_MotivoInativo_Reativa()
    {
        MotivoDeDecisao motivo = CriarValido();
        motivo.Desativar();

        motivo.Reativar().IsSuccess.Should().BeTrue();
        motivo.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Reativar motivo já ativo é recusado")]
    public void Reativar_MotivoAtivo_Recusa()
    {
        CriarValido().Reativar().Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JaAtivo);
    }

    [Fact(DisplayName = "Não há marcação de etapa: a mesma lista serve à análise inicial e ao recurso")]
    public void Motivo_NaoTemMarcacaoDeEtapa()
    {
        typeof(MotivoDeDecisao).GetProperties().Select(propriedade => propriedade.Name)
            .Should().NotContain(["Etapa", "EtapaAplicavel", "SomenteRecurso"]);
    }

    internal static MotivoDeDecisao CriarValido() =>
        Criar(TipoMotivoDeDecisao.IsencaoDaTaxa, "RENDA_ACIMA_DO_LIMITE", FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel).Value!;

    private static Result<MotivoDeDecisao> Criar(
        TipoMotivoDeDecisao tipo, string? codigo, string? subclassificacao, SentidoDoMotivo sentido, string descricao = DescricaoValida) =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(tipo, codigo, descricao, subclassificacao, sentido));
}
