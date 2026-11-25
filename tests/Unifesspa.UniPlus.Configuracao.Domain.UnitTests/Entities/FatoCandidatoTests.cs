namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

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
        Criar(definicao: Definicao with { FonteValores = null }).Error!.Code.Should().Be(FatoCandidatoErrorCodes.FonteValoresObrigatoria);

    [Fact(DisplayName = "Criar booleano com fonte dos valores é recusado")]
    public void Criar_BooleanoComFonte_Recusa() =>
        CriacaoDoFato.Criar(
            new DescritivoDoFato("PCD", "Pessoa com deficiência", "Descrição do fato."),
            OrigemFato.Declarado,
            "CAMPO_FORMULARIO:PCD",
            new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, FonteValoresFato.OpcoesDoProcesso, null, PontoResolucaoInscricao, EscopoFato.Candidato, ClassificacaoDeProtecao.Sensivel, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal),
            sistema: false)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.FonteValoresForaDeCategorico);

    [Theory(DisplayName = "Escopo ausente ou fora do vocabulário é recusado")]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("TODOS")]
    [InlineData("candidato")]
    public void Criar_EscopoInvalido_Recusa(string? codigo) =>
        Criar(definicao: Definicao with { Escopo = CodigoInformado<EscopoFato>.Ler(codigo) })
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.EscopoObrigatorio);

    [Theory(DisplayName = "Fato de texto declara formato, e só ele")]
    [InlineData(TipoDoDado.Texto, null, FatoCandidatoErrorCodes.FormatoObrigatorio)]
    [InlineData(TipoDoDado.SimOuNao, FormatoTexto.Cpf, FatoCandidatoErrorCodes.FormatoForaDeTexto)]
    public void Criar_FormatoIncoerenteComDominio_Recusa(TipoDoDado dominio, FormatoTexto? formato, string codigoEsperado) =>
        Criar(descritivo: Descritivo with { Codigo = "CPF" }, definicao: Definicao with { Dominio = dominio, Formato = formato, FonteValores = dominio == TipoDoDado.Opcao ? Definicao.FonteValores : null }, binding: "CAMPO_FORMULARIO:CPF")
            .Error!.Code.Should().Be(codigoEsperado);

    [Theory(DisplayName = "Texto, data e endereço não aceitam classificação abaixo de pessoal")]
    [InlineData(TipoDoDado.Texto, ClassificacaoDeProtecao.Interno)]
    [InlineData(TipoDoDado.Data, ClassificacaoDeProtecao.Publico)]
    [InlineData(TipoDoDado.Endereco, ClassificacaoDeProtecao.Interno)]
    public void Criar_ClassificacaoAbaixoDoMinimo_Recusa(TipoDoDado dominio, ClassificacaoDeProtecao classificacao) =>
        Criar(
            descritivo: Descritivo with { Codigo = "DADO" },
            definicao: Definicao with { Dominio = dominio, Formato = dominio == TipoDoDado.Texto ? FormatoTexto.Livre : null, ClassificacaoProtecao = classificacao, HipoteseLegal = HipoteseLegalTratamento.ExecucaoPoliticasPublicas, FonteValores = dominio == TipoDoDado.Opcao ? Definicao.FonteValores : null },
            binding: "CAMPO_FORMULARIO:DADO")
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio);

    [Fact(DisplayName = "CA-01 — fato de texto aceita a classificação Identificador")]
    public void Criar_TextoComIdentificador_Aceita() =>
        Criar(
            descritivo: Descritivo with { Codigo = "CPF" },
            definicao: Definicao with { Dominio = TipoDoDado.Texto, Formato = FormatoTexto.Cpf, ClassificacaoProtecao = ClassificacaoDeProtecao.Identificador, HipoteseLegal = HipoteseLegalTratamento.CumprimentoObrigacaoLegal, FonteValores = null },
            binding: "CAMPO_FORMULARIO:CPF")
            .IsSuccess.Should().BeTrue();

    [Theory(DisplayName = "Identificador é classificação mínima só para texto — data e endereço continuam exigindo Pessoal ou Sensível")]
    [InlineData(TipoDoDado.Data)]
    [InlineData(TipoDoDado.Endereco)]
    public void Criar_DataOuEnderecoComIdentificador_Recusa(TipoDoDado dominio) =>
        Criar(
            descritivo: Descritivo with { Codigo = "DADO" },
            definicao: Definicao with { Dominio = dominio, ClassificacaoProtecao = ClassificacaoDeProtecao.Identificador, HipoteseLegal = HipoteseLegalTratamento.ExecucaoPoliticasPublicas, FonteValores = dominio == TipoDoDado.Opcao ? Definicao.FonteValores : null },
            binding: "CAMPO_FORMULARIO:DADO")
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio);

    [Theory(DisplayName = "Identificador nunca é aceito fora do domínio texto — categórico, booleano e numérico também recusam")]
    [InlineData(TipoDoDado.Opcao)]
    [InlineData(TipoDoDado.SimOuNao)]
    [InlineData(TipoDoDado.Numerico)]
    public void Criar_DominioQueNaoEhTextoComIdentificador_Recusa(TipoDoDado dominio) =>
        Criar(
            descritivo: Descritivo with { Codigo = "DADO" },
            definicao: Definicao with { Dominio = dominio, ClassificacaoProtecao = ClassificacaoDeProtecao.Identificador, HipoteseLegal = HipoteseLegalTratamento.ExecucaoPoliticasPublicas, FonteValores = dominio == TipoDoDado.Opcao ? Definicao.FonteValores : null },
            binding: "CAMPO_FORMULARIO:DADO")
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio);

    [Theory(DisplayName = "O nome social de sistema é pessoal como todo texto: a factory recusa público e interno para ele")]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, true, ClassificacaoDeProtecao.Publico, false)]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, false, ClassificacaoDeProtecao.Publico, false)]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, true, ClassificacaoDeProtecao.Interno, false)]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, true, ClassificacaoDeProtecao.Pessoal, true)]
    [InlineData("NOME", true, ClassificacaoDeProtecao.Publico, false)]
    public void Criar_TextoAbaixoDePessoal_Recusa(string codigo, bool sistema, ClassificacaoDeProtecao classificacao, bool aceito) =>
        Criar(
            descritivo: Descritivo with { Codigo = codigo },
            definicao: Definicao with { Dominio = TipoDoDado.Texto, Formato = FormatoTexto.NomePessoa, ClassificacaoProtecao = classificacao, HipoteseLegal = HipoteseLegalTratamento.ExecucaoPoliticasPublicas, FonteValores = null },
            binding: $"CAMPO_FORMULARIO:{codigo}",
            sistema: sistema)
            .IsSuccess.Should().Be(aceito);

    [Fact(DisplayName = "Violações independentes saem juntas, cada uma no seu campo (ADR-0125)")]
    public void Criar_ViolacoesIndependentes_Acumula()
    {
        Result<FatoCandidato> resultado = Criar(
            descritivo: Descritivo with { Codigo = "codigo invalido", Nome = " " },
            definicao: Definicao with { ClassificacaoProtecao = null, FinalidadeTratamento = " " });

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(
            ["codigo", "nome", "classificacaoProtecao", "finalidadeTratamento"]);
    }

    [Fact(DisplayName = "Fato do administrador é declarado, com vínculo ao campo gerado a partir do código, e não é de sistema")]
    public void CriarDoAdministrador_DeclaradoComVinculoDoCodigo()
    {
        FatoCandidato fato = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato("ANO_CONCLUSAO", "Ano de conclusão", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico, null, null, PontoResolucaoInscricao, EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;

        fato.Origem.Should().Be(OrigemFato.Declarado);
        fato.Binding.Should().Be("CAMPO_FORMULARIO:ANO_CONCLUSAO");
        fato.SemeadoPeloSistema.Should().BeFalse();
    }

    [Fact(DisplayName = "Editar nome, descrição e componente acumula as três violações")]
    public void AlterarDescritivo_AcumulaViolacoes() =>
        Criar().Value!.AlterarDescritivo(" ", new string('x', 1001), CodigoInformado<ComponenteDeExibicao>.Ler("INEXISTENTE"))
            .Errors.Select(static e => e.Field).Should().BeEquivalentTo(["nome", "descricao", "componenteExibicao"]);

    [Theory(DisplayName = "Descrição em branco é recusada no cadastro e na edição do fato")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DescricaoEmBranco_Recusa(string? descricao)
    {
        Criar(descritivo: Descritivo with { Descricao = descricao }).Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.DescricaoObrigatoria);

        Criar().Value!.AlterarDescritivo("Cor ou raça", descricao, null).Error!.Code
            .Should().Be(FatoCandidatoErrorCodes.DescricaoObrigatoria);
    }

    [Theory(DisplayName = "Componente de exibição fora do vocabulário é recusado no cadastro; o declarado é aceito")]
    [InlineData("INEXISTENTE", false)]
    [InlineData("moeda", false)]
    [InlineData("MOEDA", true)]
    public void ComponenteDeExibicao_SoDoVocabularioFechado(string codigo, bool aceito)
    {
        Result<FatoCandidato> resultado = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato("RENDA_BRUTA_FAMILIAR", "Renda bruta familiar", Descricao, CodigoInformado<ComponenteDeExibicao>.Ler(codigo)),
            new DefinicaoDoFato(TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico, null, null, PontoResolucaoInscricao, EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal));

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
    [InlineData(null, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal, FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria)]
    [InlineData(ClassificacaoDeProtecao.Pessoal, "  ", HipoteseLegalTratamento.CumprimentoObrigacaoLegal, FatoCandidatoErrorCodes.FinalidadeTratamentoObrigatoria)]
    [InlineData(ClassificacaoDeProtecao.Pessoal, Finalidade, null, FatoCandidatoErrorCodes.HipoteseLegalObrigatoria)]
    public void Criar_ProtecaoIncompleta_Recusa(
        ClassificacaoDeProtecao? classificacao, string finalidade, HipoteseLegalTratamento? hipotese, string codigoEsperado) =>
        Criar(definicao: Definicao with { ClassificacaoProtecao = classificacao, FinalidadeTratamento = finalidade, HipoteseLegal = hipotese })
            .Error!.Code.Should().Be(codigoEsperado);

    [Theory(DisplayName = "Hipótese legal fora do artigo da classificação é recusada: art. 11 para sensível, art. 7º para os demais")]
    [InlineData(ClassificacaoDeProtecao.Sensivel, HipoteseLegalTratamento.InteresseLegitimo)]
    [InlineData(ClassificacaoDeProtecao.Sensivel, HipoteseLegalTratamento.ExecucaoContrato)]
    [InlineData(ClassificacaoDeProtecao.Pessoal, HipoteseLegalTratamento.PrevencaoAFraude)]
    public void Criar_HipoteseForaDoArtigo_Recusa(ClassificacaoDeProtecao classificacao, HipoteseLegalTratamento hipotese) =>
        Criar(definicao: Definicao with { ClassificacaoProtecao = classificacao, HipoteseLegal = hipotese })
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
        FatoCandidato fato = Criar(descritivo: Descritivo with { Descricao = "Cor ou raça autodeclarada" }).Value!;

        fato.Id.Should().NotBe(Guid.Empty);
        fato.Codigo.Should().Be("COR_RACA");
        fato.Nome.Should().Be("Cor ou raça");
        fato.Descricao.Should().Be("Cor ou raça autodeclarada");
        fato.Dominio.Should().Be(TipoDoDado.Opcao);
        fato.Origem.Should().Be(OrigemFato.Declarado);
        fato.Cardinalidade.Should().Be(QuantidadeDeValores.ValorUnico);
        fato.PontoResolucao.Should().Be("INSCRICAO");
        fato.Binding.Should().Be(BindingCorRaca);
    }

    [Theory(DisplayName = "Código ausente, em branco ou fora do formato é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("cor_raca")]      // minúsculo
    [InlineData("1COR")]          // começa por dígito
    [InlineData("COR-RACA")]      // hífen
    [InlineData("A__B")]
    public void Criar_CodigoInvalido_Falha(string codigo)
    {
        Result<FatoCandidato> resultado = Criar(descritivo: Descritivo with { Codigo = codigo });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().StartWith("FatoCandidato.Codigo");
    }

    [Fact(DisplayName = "Nome ausente é rejeitado")]
    public void Criar_SemNome_Falha()
    {
        Result<FatoCandidato> resultado = Criar(descritivo: Descritivo with { Nome = "   " });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Domínio ausente é rejeitado")]
    public void Criar_DominioAusente_Falha()
    {
        Result<FatoCandidato> resultado = Criar(definicao: Definicao with { Dominio = null, FonteValores = null });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.DominioObrigatorio);
    }

    [Fact(DisplayName = "Domínio com código desconhecido é rejeitado como inválido, não aceito")]
    public void Criar_DominioDesconhecido_Falha()
    {
        Result<FatoCandidato> resultado = Criar(definicao: Definicao with { Dominio = CodigoInformado<TipoDoDado>.Ler("INEXISTENTE"), FonteValores = null });

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

    [Fact(DisplayName = "Cardinalidade com código desconhecido é rejeitada como inválida")]
    public void Criar_CardinalidadeDesconhecida_Falha()
    {
        Result<FatoCandidato> resultado = Criar(definicao: Definicao with { Cardinalidade = CodigoInformado<QuantidadeDeValores>.Ler("INEXISTENTE") });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.CardinalidadeInvalida);
    }

    [Fact(DisplayName = "Origem ausente é rejeitada")]
    public void Criar_OrigemAusente_Falha()
    {
        Result<FatoCandidato> resultado = Criar(origem: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.OrigemObrigatoria);
    }

    [Fact(DisplayName = "Cardinalidade ausente é rejeitada")]
    public void Criar_CardinalidadeAusente_Falha()
    {
        Result<FatoCandidato> resultado = Criar(definicao: Definicao with { Cardinalidade = null });

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
