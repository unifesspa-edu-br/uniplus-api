namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.FatoCandidatoDeTeste;

/// <summary>
/// Invariantes de domínio do catálogo <c>FatoCandidato</c> (ADR-0136): a factory valida código,
/// domínio, origem, cardinalidade, fonte dos valores, escopo e proteção de dados; o fato de sistema só
/// tem nome, descrição, componente de exibição e proteção editáveis.
/// </summary>
public sealed class FatoCandidatoTests
{
    [Fact(DisplayName = "Criar categórico sem fonte dos valores é recusado")]
    public void Criar_CategoricoSemFonte_Recusa() =>
        Criar(fonteValores: null).Error!.Code.Should().Be(FatoCandidatoErrorCodes.FonteValoresObrigatoria);

    [Fact(DisplayName = "Criar booleano com fonte dos valores é recusado")]
    public void Criar_BooleanoComFonte_Recusa() =>
        FatoCandidato.Criar("PCD", "Pessoa com deficiência", "Descrição do fato.", DominioFato.Booleano, OrigemFato.Declarado,
            CardinalidadeFato.Escalar, FonteValoresFato.Processo, formato: null, PontoResolucaoInscricao, "CAMPO_FORMULARIO:PCD",
            EscopoFato.Candidato, ClassificacaoDeProtecao.Sensivel, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal,
            sistema: false)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.FonteValoresForaDeCategorico);

    [Theory(DisplayName = "Escopo ausente ou fora do vocabulário é recusado")]
    [InlineData(EscopoFato.Nenhum)]
    [InlineData((EscopoFato)999)]
    public void Criar_EscopoInvalido_Recusa(EscopoFato escopo) =>
        Criar(escopo: escopo).Error!.Code.Should().Be(FatoCandidatoErrorCodes.EscopoObrigatorio);

    [Theory(DisplayName = "Fato de texto declara formato, e só ele")]
    [InlineData(DominioFato.Texto, null, FatoCandidatoErrorCodes.FormatoObrigatorio)]
    [InlineData(DominioFato.Booleano, FormatoTexto.Cpf, FatoCandidatoErrorCodes.FormatoForaDeTexto)]
    public void Criar_FormatoIncoerenteComDominio_Recusa(DominioFato dominio, FormatoTexto? formato, string codigoEsperado) =>
        Criar(codigo: "CPF", dominio: dominio, formato: formato, binding: "CAMPO_FORMULARIO:CPF")
            .Error!.Code.Should().Be(codigoEsperado);

    [Theory(DisplayName = "Texto, data e endereço não aceitam classificação abaixo de pessoal")]
    [InlineData(DominioFato.Texto, ClassificacaoDeProtecao.Interno)]
    [InlineData(DominioFato.Data, ClassificacaoDeProtecao.Publico)]
    [InlineData(DominioFato.Endereco, ClassificacaoDeProtecao.Interno)]
    public void Criar_ClassificacaoAbaixoDoMinimo_Recusa(DominioFato dominio, ClassificacaoDeProtecao classificacao) =>
        Criar(
            codigo: "DADO", dominio: dominio, formato: dominio == DominioFato.Texto ? FormatoTexto.Livre : null,
            binding: "CAMPO_FORMULARIO:DADO", classificacao: classificacao, hipotese: HipoteseLegalTratamento.ExecucaoPoliticasPublicas)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio);

    [Fact(DisplayName = "CA-01 — fato de texto aceita a classificação Identificador")]
    public void Criar_TextoComIdentificador_Aceita() =>
        Criar(
            codigo: "CPF", dominio: DominioFato.Texto, formato: FormatoTexto.Cpf, binding: "CAMPO_FORMULARIO:CPF",
            classificacao: ClassificacaoDeProtecao.Identificador, hipotese: HipoteseLegalTratamento.CumprimentoObrigacaoLegal)
            .IsSuccess.Should().BeTrue();

    [Theory(DisplayName = "Identificador é classificação mínima só para texto — data e endereço continuam exigindo Pessoal ou Sensível")]
    [InlineData(DominioFato.Data)]
    [InlineData(DominioFato.Endereco)]
    public void Criar_DataOuEnderecoComIdentificador_Recusa(DominioFato dominio) =>
        Criar(
            codigo: "DADO", dominio: dominio, binding: "CAMPO_FORMULARIO:DADO",
            classificacao: ClassificacaoDeProtecao.Identificador, hipotese: HipoteseLegalTratamento.ExecucaoPoliticasPublicas)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio);

    [Theory(DisplayName = "Identificador nunca é aceito fora do domínio texto — categórico, booleano e numérico também recusam")]
    [InlineData(DominioFato.Categorico)]
    [InlineData(DominioFato.Booleano)]
    [InlineData(DominioFato.Numerico)]
    public void Criar_DominioQueNaoEhTextoComIdentificador_Recusa(DominioFato dominio) =>
        Criar(
            codigo: "DADO", dominio: dominio, binding: "CAMPO_FORMULARIO:DADO",
            classificacao: ClassificacaoDeProtecao.Identificador, hipotese: HipoteseLegalTratamento.ExecucaoPoliticasPublicas)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio);

    [Theory(DisplayName = "O nome social de sistema é pessoal como todo texto: a factory recusa público e interno para ele")]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, true, ClassificacaoDeProtecao.Publico, false)]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, false, ClassificacaoDeProtecao.Publico, false)]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, true, ClassificacaoDeProtecao.Interno, false)]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, true, ClassificacaoDeProtecao.Pessoal, true)]
    [InlineData("NOME", true, ClassificacaoDeProtecao.Publico, false)]
    public void Criar_TextoAbaixoDePessoal_Recusa(string codigo, bool sistema, ClassificacaoDeProtecao classificacao, bool aceito) =>
        Criar(
            codigo: codigo, dominio: DominioFato.Texto, formato: FormatoTexto.NomePessoa, binding: $"CAMPO_FORMULARIO:{codigo}",
            classificacao: classificacao, hipotese: HipoteseLegalTratamento.ExecucaoPoliticasPublicas, sistema: sistema)
            .IsSuccess.Should().Be(aceito);

    [Fact(DisplayName = "Violações independentes saem juntas, cada uma no seu campo (ADR-0125)")]
    public void Criar_ViolacoesIndependentes_Acumula()
    {
        Result<FatoCandidato> resultado = Criar(
            codigo: "codigo invalido", nome: " ", classificacao: ClassificacaoDeProtecao.Nenhuma, finalidade: " ");

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(
            ["codigo", "nome", "classificacaoProtecao", "finalidadeTratamento"]);
    }

    [Fact(DisplayName = "Fato do administrador é declarado, com vínculo ao campo gerado a partir do código, e não é de sistema")]
    public void CriarDoAdministrador_DeclaradoComVinculoDoCodigo()
    {
        FatoCandidato fato = FatoCandidato.CriarDoAdministrador(
            " ANO_CONCLUSAO ", "Ano de conclusão", "Descrição do fato.", DominioFato.Numerico, CardinalidadeFato.Escalar, null, null,
            PontoResolucaoInscricao, EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade,
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

        fato.Origem.Should().Be(OrigemFato.Declarado);
        fato.Binding.Should().Be("CAMPO_FORMULARIO:ANO_CONCLUSAO");
        fato.SemeadoPeloSistema.Should().BeFalse();
    }

    [Fact(DisplayName = "Editar nome, descrição e componente acumula as três violações")]
    public void AlterarDescritivo_AcumulaViolacoes() =>
        Criar().Value!.AlterarDescritivo(" ", new string('x', 1001), (ComponenteDeExibicao)(-1))
            .Errors.Select(static e => e.Field).Should().BeEquivalentTo(["nome", "descricao", "componenteExibicao"]);

    [Theory(DisplayName = "Descrição em branco é recusada no cadastro e na edição do fato")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DescricaoEmBranco_Recusa(string? descricao)
    {
        Criar(descricao: descricao).Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.DescricaoObrigatoria);

        Criar().Value!.AlterarDescritivo("Cor ou raça", descricao, null).Error!.Code
            .Should().Be(FatoCandidatoErrorCodes.DescricaoObrigatoria);
    }

    [Theory(DisplayName = "Componente de exibição fora do vocabulário é recusado no cadastro; o declarado é aceito")]
    [InlineData(ComponenteDeExibicao.Nenhum, false)]
    [InlineData((ComponenteDeExibicao)99, false)]
    [InlineData(ComponenteDeExibicao.Moeda, true)]
    public void ComponenteDeExibicao_SoDoVocabularioFechado(ComponenteDeExibicao componente, bool aceito)
    {
        Result<FatoCandidato> resultado = FatoCandidato.CriarDoAdministrador(
            "RENDA_BRUTA_FAMILIAR", "Renda bruta familiar", Descricao, DominioFato.Numerico, CardinalidadeFato.Escalar, null, null,
            PontoResolucaoInscricao, EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade,
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal, componente);

        resultado.IsSuccess.Should().Be(aceito);
        if (aceito)
        {
            resultado.Value!.ComponenteExibicao.Should().Be(ComponenteDeExibicao.Moeda);
        }
        else
        {
            resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.ComponenteExibicaoInvalido);
        }
    }

    [Theory(DisplayName = "Proteção de dados incompleta é recusada")]
    [InlineData(ClassificacaoDeProtecao.Nenhuma, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal, FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria)]
    [InlineData(ClassificacaoDeProtecao.Pessoal, "  ", HipoteseLegalTratamento.CumprimentoObrigacaoLegal, FatoCandidatoErrorCodes.FinalidadeTratamentoObrigatoria)]
    [InlineData(ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.Nenhuma, FatoCandidatoErrorCodes.HipoteseLegalObrigatoria)]
    public void Criar_ProtecaoIncompleta_Recusa(
        ClassificacaoDeProtecao classificacao, string finalidade, HipoteseLegalTratamento hipotese, string codigoEsperado) =>
        Criar(classificacao: classificacao, finalidade: finalidade, hipotese: hipotese)
            .Error!.Code.Should().Be(codigoEsperado);

    [Theory(DisplayName = "Hipótese legal fora do artigo da classificação é recusada: art. 11 para sensível, art. 7º para os demais")]
    [InlineData(ClassificacaoDeProtecao.Sensivel, HipoteseLegalTratamento.InteresseLegitimo)]
    [InlineData(ClassificacaoDeProtecao.Sensivel, HipoteseLegalTratamento.ExecucaoContrato)]
    [InlineData(ClassificacaoDeProtecao.Pessoal, HipoteseLegalTratamento.PrevencaoAFraude)]
    public void Criar_HipoteseForaDoArtigo_Recusa(ClassificacaoDeProtecao classificacao, HipoteseLegalTratamento hipotese) =>
        Criar(classificacao: classificacao, hipotese: hipotese)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.HipoteseLegalIncompativelComClassificacao);

    [Fact(DisplayName = "Fato de sistema edita nome, descrição e componente, mas não é desativado nem recebe valor")]
    public void FatoDeSistema_EditaNomeEDescricao_NaoDesativa()
    {
        FatoCandidato fato = Criar(sistema: true).Value!;

        fato.AlterarDescritivo("Cor ou raça autodeclarada", "Conforme o IBGE", ComponenteDeExibicao.Situacao).IsSuccess.Should().BeTrue();
        fato.ComponenteExibicao.Should().Be(ComponenteDeExibicao.Situacao);
        fato.Desativar().Error!.Code.Should().Be(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);
        fato.AdicionarValorDominio("NOVA", "Nova", 9, ativo: true)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);
        fato.Nome.Should().Be("Cor ou raça autodeclarada");
        fato.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Desativar e reativar recusam repetir o estado")]
    public void DesativarReativar_RecusaRepetirEstado()
    {
        FatoCandidato fato = Criar().Value!;

        fato.Reativar().Error!.Code.Should().Be(FatoCandidatoErrorCodes.JaAtivo);
        fato.Desativar().IsSuccess.Should().BeTrue();
        fato.Desativar().Error!.Code.Should().Be(FatoCandidatoErrorCodes.JaDesativado);
        fato.Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar categórico válido preenche os campos com Guid v7")]
    public void Criar_CategoricoValido_Preenche()
    {
        FatoCandidato fato = Criar(descricao: "Cor ou raça autodeclarada").Value!;

        fato.Id.Should().NotBe(Guid.Empty);
        fato.Codigo.Should().Be("COR_RACA");
        fato.Nome.Should().Be("Cor ou raça");
        fato.Descricao.Should().Be("Cor ou raça autodeclarada");
        fato.Dominio.Should().Be(DominioFato.Categorico);
        fato.Origem.Should().Be(OrigemFato.Declarado);
        fato.Cardinalidade.Should().Be(CardinalidadeFato.Escalar);
        fato.PontoResolucao.Should().Be("INSCRICAO");
        fato.Binding.Should().Be(BindingCorRaca);
    }

    [Theory(DisplayName = "Código ausente, em branco ou fora do formato é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("cor_raca")]      // minúsculo
    [InlineData("1COR")]          // começa por dígito
    [InlineData("COR-RACA")]      // hífen
    [InlineData("A")]             // curto demais (< 2)
    public void Criar_CodigoInvalido_Falha(string codigo)
    {
        Result<FatoCandidato> resultado = Criar(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().StartWith("FatoCandidato.Codigo");
    }

    [Fact(DisplayName = "Nome ausente é rejeitado")]
    public void Criar_SemNome_Falha()
    {
        Result<FatoCandidato> resultado = Criar(nome: "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Domínio Nenhum (não decidível — ex.: 'texto') é rejeitado")]
    public void Criar_DominioNenhum_Falha()
    {
        Result<FatoCandidato> resultado = Criar(dominio: DominioFato.Nenhum);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.DominioObrigatorio);
    }

    [Fact(DisplayName = "Domínio fora do roster (cast inválido) é rejeitado como inválido, não aceito")]
    public void Criar_DominioForaDoRoster_Falha()
    {
        Result<FatoCandidato> resultado = Criar(dominio: (DominioFato)999);

        resultado.IsFailure.Should().BeTrue("a factory não pode delegar a rejeição ao converter (que lançaria 500)");
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.DominioInvalido);
    }

    [Fact(DisplayName = "Origem fora do roster (cast inválido) é rejeitada como inválida")]
    public void Criar_OrigemForaDoRoster_Falha()
    {
        Result<FatoCandidato> resultado = Criar(origem: (OrigemFato)999);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.OrigemInvalida);
    }

    [Fact(DisplayName = "Cardinalidade fora do roster (cast inválido) é rejeitada como inválida")]
    public void Criar_CardinalidadeForaDoRoster_Falha()
    {
        Result<FatoCandidato> resultado = Criar(cardinalidade: (CardinalidadeFato)999);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.CardinalidadeInvalida);
    }

    [Fact(DisplayName = "Origem Nenhuma é rejeitada")]
    public void Criar_OrigemNenhuma_Falha()
    {
        Result<FatoCandidato> resultado = Criar(origem: OrigemFato.Nenhuma);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.OrigemObrigatoria);
    }

    [Fact(DisplayName = "Cardinalidade Nenhuma é rejeitada")]
    public void Criar_CardinalidadeNenhuma_Falha()
    {
        Result<FatoCandidato> resultado = Criar(cardinalidade: CardinalidadeFato.Nenhuma);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.CardinalidadeObrigatoria);
    }

    // ─── PontoResolucao (ADR-0116) ──────────────────────────────────────────

    [Fact(DisplayName = "FatoCandidato deriva de EntityBase puro — não é soft-deletable")]
    public void FatoCandidato_EhEntityBasePuro()
    {
        typeof(EntityBase).IsAssignableFrom(typeof(FatoCandidato)).Should().BeTrue();
        typeof(ISoftDeletable).IsAssignableFrom(typeof(FatoCandidato)).Should().BeFalse(
            "o fato é desativado, nunca removido logicamente");
    }

    [Fact(DisplayName = "Todas as propriedades do FatoCandidato têm setter não-público (imutável)")]
    public void FatoCandidato_PropriedadesImutaveis()
    {
        PropertyInfo[] propriedades = typeof(FatoCandidato)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo propriedade in propriedades)
        {
            MethodInfo? setter = propriedade.GetSetMethod(nonPublic: false);
            setter.Should().BeNull($"a propriedade '{propriedade.Name}' não pode ter setter público (entidade imutável)");
        }
    }
}
