namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Invariantes de domínio do catálogo <c>FatoCandidato</c> (ADR-0136): a factory valida código,
/// domínio, origem, cardinalidade, fonte dos valores, ponto de resolução, binding, escopo e
/// proteção de dados; o fato de sistema só tem nome e descrição editáveis.
/// </summary>
public sealed class FatoCandidatoTests
{
    private const string BindingCorRaca = "CAMPO_FORMULARIO:COR_RACA";
    private const string PontoResolucaoInscricao = "INSCRICAO";

    private const string Finalidade = "Enquadramento na reserva de vagas.";

    private static Result<FatoCandidato> Criar(
        string codigo = "COR_RACA",
        string nome = "Cor ou raça",
        string? descricao = null,
        DominioFato dominio = DominioFato.Categorico,
        OrigemFato origem = OrigemFato.Declarado,
        CardinalidadeFato cardinalidade = CardinalidadeFato.Escalar,
        FonteValoresFato? fonteValores = FonteValoresFato.Global,
        FormatoTexto? formato = null,
        string pontoResolucao = PontoResolucaoInscricao,
        string binding = BindingCorRaca,
        ClassificacaoProtecaoDado classificacao = ClassificacaoProtecaoDado.Sensivel,
        string finalidade = Finalidade,
        HipoteseLegalTratamento hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal,
        bool sistema = false,
        EscopoFato escopo = EscopoFato.Candidato) =>
        FatoCandidato.Criar(
            codigo, nome, descricao, dominio, origem, cardinalidade,
            dominio == DominioFato.Categorico ? fonteValores : null,
            formato,
            pontoResolucao, binding, escopo, classificacao, finalidade, hipotese, sistema);

    [Fact(DisplayName = "Criar categórico sem fonte dos valores é recusado")]
    public void Criar_CategoricoSemFonte_Recusa() =>
        Criar(fonteValores: null).Error!.Code.Should().Be(FatoCandidatoErrorCodes.FonteValoresObrigatoria);

    [Fact(DisplayName = "Criar booleano com fonte dos valores é recusado")]
    public void Criar_BooleanoComFonte_Recusa() =>
        FatoCandidato.Criar("PCD", "Pessoa com deficiência", null, DominioFato.Booleano, OrigemFato.Declarado,
            CardinalidadeFato.Escalar, FonteValoresFato.Processo, formato: null, PontoResolucaoInscricao, "CAMPO_FORMULARIO:PCD",
            EscopoFato.Candidato, ClassificacaoProtecaoDado.Sensivel, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal,
            sistema: false)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.FonteValoresForaDeCategorico);

    [Fact(DisplayName = "Valor de domínio em fato de fonte do processo é recusado")]
    public void AdicionarValorDominio_FonteProcesso_Recusa()
    {
        FatoCandidato fato = Criar(
            codigo: "TIPO_DEFICIENCIA", fonteValores: FonteValoresFato.Processo, binding: "CAMPO_FORMULARIO:TIPO_DEFICIENCIA").Value!;

        fato.AdicionarValorDominio("VISUAL", "Deficiência visual", 0, ativo: true)
            .Error!.Code.Should().Be(FatoValorDominioErrorCodes.NaoPermitidoForaDeFonteGlobal);
    }

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
    [InlineData(DominioFato.Texto, ClassificacaoProtecaoDado.Interno)]
    [InlineData(DominioFato.Data, ClassificacaoProtecaoDado.Publico)]
    [InlineData(DominioFato.Endereco, ClassificacaoProtecaoDado.Interno)]
    public void Criar_ClassificacaoAbaixoDoMinimo_Recusa(DominioFato dominio, ClassificacaoProtecaoDado classificacao) =>
        Criar(
            codigo: "DADO", dominio: dominio, formato: dominio == DominioFato.Texto ? FormatoTexto.Livre : null,
            binding: "CAMPO_FORMULARIO:DADO", classificacao: classificacao, hipotese: HipoteseLegalTratamento.ExecucaoPoliticasPublicas)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio);

    [Theory(DisplayName = "Só o nome social de sistema é texto público; outro texto de sistema, o nome social do administrador ou outra classificação abaixo de pessoal, não")]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, true, ClassificacaoProtecaoDado.Publico, true)]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, false, ClassificacaoProtecaoDado.Publico, false)]
    [InlineData(FatoCandidato.CodigoDoNomeSocial, true, ClassificacaoProtecaoDado.Interno, false)]
    [InlineData("NOME", true, ClassificacaoProtecaoDado.Publico, false)]
    public void Criar_TextoAbaixoDePessoal_SoONomeSocialPublicoDeSistema(string codigo, bool sistema, ClassificacaoProtecaoDado classificacao, bool aceito) =>
        Criar(
            codigo: codigo, dominio: DominioFato.Texto, formato: FormatoTexto.NomePessoa, binding: $"CAMPO_FORMULARIO:{codigo}",
            classificacao: classificacao, hipotese: HipoteseLegalTratamento.ExecucaoPoliticasPublicas, sistema: sistema)
            .IsSuccess.Should().Be(aceito);

    [Fact(DisplayName = "Violações independentes saem juntas, cada uma no seu campo (ADR-0125)")]
    public void Criar_ViolacoesIndependentes_Acumula()
    {
        Result<FatoCandidato> resultado = Criar(
            codigo: "codigo invalido", nome: " ", classificacao: ClassificacaoProtecaoDado.Nenhuma, finalidade: " ");

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(
            ["codigo", "nome", "classificacaoProtecao", "finalidadeTratamento"]);
    }

    [Fact(DisplayName = "Fato do administrador é declarado, com vínculo ao campo gerado a partir do código, e não é de sistema")]
    public void CriarDoAdministrador_DeclaradoComVinculoDoCodigo()
    {
        FatoCandidato fato = FatoCandidato.CriarDoAdministrador(
            " ANO_CONCLUSAO ", "Ano de conclusão", null, DominioFato.Numerico, CardinalidadeFato.Escalar, null, null,
            PontoResolucaoInscricao, EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal, Finalidade,
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

        fato.Origem.Should().Be(OrigemFato.Declarado);
        fato.Binding.Should().Be("CAMPO_FORMULARIO:ANO_CONCLUSAO");
        fato.Sistema.Should().BeFalse();
    }

    [Fact(DisplayName = "Acrescentar valor acumula as violações do código, da descrição e da ordem")]
    public void AdicionarValorDominio_AcumulaViolacoes() =>
        Criar().Value!.AdicionarValorDominio(" ", null, -1, ativo: true)
            .Errors.Select(static e => e.Field).Should().BeEquivalentTo(["codigo", "descricao", "ordem"]);

    [Fact(DisplayName = "Editar nome e descrição acumula as duas violações")]
    public void AlterarDescritivo_AcumulaViolacoes() =>
        Criar().Value!.AlterarDescritivo(" ", new string('x', 1001))
            .Errors.Select(static e => e.Field).Should().BeEquivalentTo(["nome", "descricao"]);

    [Fact(DisplayName = "Valor do fato do administrador desativa e reativa; o do fato de sistema, não")]
    public void DesativarValor_SoNoFatoDoAdministrador()
    {
        FatoCandidato doAdministrador = Criar().Value!;
        doAdministrador.AdicionarValorDominio("PRETA", "Preta", 0, ativo: true).IsSuccess.Should().BeTrue();

        doAdministrador.DesativarValor("PRETA").IsSuccess.Should().BeTrue();
        doAdministrador.DesativarValor("PRETA").Error!.Code.Should().Be(FatoValorDominioErrorCodes.JaDesativado);
        doAdministrador.ReativarValor("PRETA").IsSuccess.Should().BeTrue();
        doAdministrador.DesativarValor("INEXISTENTE").Error!.Code.Should().Be(FatoValorDominioErrorCodes.NaoEncontrado);

        Criar(sistema: true).Value!.DesativarValor("PRETA")
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.FatoDeSistemaSoEditaNomeEDescricao);
    }

    [Theory(DisplayName = "Proteção de dados incompleta é recusada")]
    [InlineData(ClassificacaoProtecaoDado.Nenhuma, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal, FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria)]
    [InlineData(ClassificacaoProtecaoDado.Pessoal, "  ", HipoteseLegalTratamento.CumprimentoObrigacaoLegal, FatoCandidatoErrorCodes.FinalidadeTratamentoObrigatoria)]
    [InlineData(ClassificacaoProtecaoDado.Pessoal, Finalidade, HipoteseLegalTratamento.Nenhuma, FatoCandidatoErrorCodes.HipoteseLegalObrigatoria)]
    public void Criar_ProtecaoIncompleta_Recusa(
        ClassificacaoProtecaoDado classificacao, string finalidade, HipoteseLegalTratamento hipotese, string codigoEsperado) =>
        Criar(classificacao: classificacao, finalidade: finalidade, hipotese: hipotese)
            .Error!.Code.Should().Be(codigoEsperado);

    [Theory(DisplayName = "Hipótese legal fora do artigo da classificação é recusada: art. 11 para sensível, art. 7º para os demais")]
    [InlineData(ClassificacaoProtecaoDado.Sensivel, HipoteseLegalTratamento.InteresseLegitimo)]
    [InlineData(ClassificacaoProtecaoDado.Sensivel, HipoteseLegalTratamento.ExecucaoContrato)]
    [InlineData(ClassificacaoProtecaoDado.Pessoal, HipoteseLegalTratamento.PrevencaoAFraude)]
    public void Criar_HipoteseForaDoArtigo_Recusa(ClassificacaoProtecaoDado classificacao, HipoteseLegalTratamento hipotese) =>
        Criar(classificacao: classificacao, hipotese: hipotese)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.HipoteseLegalIncompativelComClassificacao);

    [Theory(DisplayName = "Fato do administrador não usa vínculo que exige código do sistema")]
    [InlineData(OrigemFato.Derivado, "ATRIBUTO_CANDIDATO:DADO")]
    [InlineData(OrigemFato.Derivado, "CLASSIFICACAO:DADO")]
    [InlineData(OrigemFato.Integracao, "INTEGRACAO:DADO")]
    public void Criar_FatoDoAdministradorComVinculoDeSistema_Recusa(OrigemFato origem, string binding) =>
        Criar(codigo: "DADO", origem: origem, binding: binding)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.VinculoExclusivoDeFatoDeSistema);

    [Fact(DisplayName = "Fato de sistema edita nome e descrição, mas não é desativado nem recebe valor")]
    public void FatoDeSistema_EditaNomeEDescricao_NaoDesativa()
    {
        FatoCandidato fato = Criar(sistema: true).Value!;

        fato.AlterarDescritivo("Cor ou raça autodeclarada", "Conforme o IBGE").IsSuccess.Should().BeTrue();
        fato.Desativar().Error!.Code.Should().Be(FatoCandidatoErrorCodes.FatoDeSistemaSoEditaNomeEDescricao);
        fato.AdicionarValorDominio("NOVA", "Nova", 9, ativo: true)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.FatoDeSistemaSoEditaNomeEDescricao);
        fato.Nome.Should().Be("Cor ou raça autodeclarada");
        fato.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Desativar e reativar recusam repetir o estado")]
    public void DesativarReativar_RecusaRepetirEstado()
    {
        FatoCandidato fato = Criar().Value!;

        fato.Ativar().Error!.Code.Should().Be(FatoCandidatoErrorCodes.JaAtivo);
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

    [Theory(DisplayName = "Ponto de resolução ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_PontoResolucaoAusente_Falha(string ponto)
    {
        Result<FatoCandidato> resultado = Criar(pontoResolucao: ponto);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoObrigatorio);
    }

    [Fact(DisplayName = "Ponto de resolução fora do conjunto canônico de fases é rejeitado")]
    public void Criar_PontoResolucaoForaDoCanonico_Falha()
    {
        Result<FatoCandidato> resultado = Criar(pontoResolucao: "FASE_INEXISTENTE");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoInvalido);
    }

    // ─── Binding (ADR-0116) ─────────────────────────────────────────────────

    [Theory(DisplayName = "Binding ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_BindingAusente_Falha(string binding)
    {
        Result<FatoCandidato> resultado = Criar(binding: binding);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingObrigatorio);
    }

    [Theory(DisplayName = "Binding sem separador, ou com prefixo/referência vazios, é rejeitado como formato inválido")]
    [InlineData("CAMPO_FORMULARIO_COR_RACA")]
    [InlineData(":COR_RACA")]
    [InlineData("CAMPO_FORMULARIO:")]
    public void Criar_BindingFormatoInvalido_Falha(string binding)
    {
        Result<FatoCandidato> resultado = Criar(binding: binding);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingFormatoInvalido);
    }

    [Theory(DisplayName = "Binding com prefixo incoerente com a origem é rejeitado")]
    [InlineData(OrigemFato.Declarado, "ATRIBUTO_CANDIDATO:COR_RACA")]
    [InlineData(OrigemFato.Declarado, "REGRA_DERIVACAO:MODALIDADE")]
    [InlineData(OrigemFato.Derivado, "CAMPO_FORMULARIO:FAIXA_ETARIA")]
    [InlineData(OrigemFato.Integracao, "CAMPO_FORMULARIO:ANO_ENEM")]
    [InlineData(OrigemFato.Integracao, "REGRA_DERIVACAO:ALGO")]
    public void Criar_BindingPrefixoIncoerenteComOrigem_Falha(OrigemFato origem, string binding)
    {
        Result<FatoCandidato> resultado = Criar(origem: origem, binding: binding);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingPrefixoIncoerenteComOrigem);
    }

    [Fact(DisplayName = "REGRA_DERIVACAO cuja referência não é o código do próprio fato é recusado")]
    public void Criar_BindingRegraDerivacaoReferenciaOutroFato_Falha()
    {
        Result<FatoCandidato> resultado = Criar(
            codigo: "FAIXA_ETARIA", origem: OrigemFato.Derivado, binding: "REGRA_DERIVACAO:MODALIDADE");

        resultado.IsFailure.Should().BeTrue(
            "a regra de derivação referenciada é a do próprio fato — apontar para outro congelaria metadado de derivação alheio");
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingReferenciaRegraIncoerente);
    }

    [Fact(DisplayName = "REGRA_DERIVACAO cuja referência é o código do próprio fato é aceito")]
    public void Criar_BindingRegraDerivacaoReferenciaProprioFato_Aceita()
    {
        Result<FatoCandidato> resultado = Criar(
            codigo: "MODALIDADE", dominio: DominioFato.Categorico,
            cardinalidade: CardinalidadeFato.Multivalorado,
            origem: OrigemFato.Derivado, binding: "REGRA_DERIVACAO:MODALIDADE");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A mensagem de prefixo incoerente lista os dois prefixos aceitos quando a origem é derivada")]
    public void Criar_BindingPrefixoIncoerente_Derivado_MensagemListaOsDois()
    {
        Result<FatoCandidato> resultado = Criar(origem: OrigemFato.Derivado, binding: "CAMPO_FORMULARIO:X");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Message.Should().Contain("ATRIBUTO_CANDIDATO")
            .And.Contain("REGRA_DERIVACAO",
                "a origem derivada aceita dois prefixos — a mensagem precisa apontar ambos, não um só");
    }

    [Theory(DisplayName = "Binding com prefixo coerente com a origem é aceito")]
    [InlineData(OrigemFato.Declarado, "CAMPO_FORMULARIO:COR_RACA")]
    [InlineData(OrigemFato.Derivado, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA")]
    // REGRA_DERIVACAO referencia o próprio fato: a referência bate com o código FATO_QUALQUER.
    [InlineData(OrigemFato.Derivado, "REGRA_DERIVACAO:FATO_QUALQUER")]
    [InlineData(OrigemFato.Integracao, "INTEGRACAO:ANO_ENEM")]
    public void Criar_BindingPrefixoCoerenteComOrigem_Aceita(OrigemFato origem, string binding)
    {
        // Atributo do candidato e integração só existem em fato de sistema.
        Result<FatoCandidato> resultado = Criar(
            codigo: "FATO_QUALQUER", dominio: DominioFato.Numerico,
            origem: origem, binding: binding, sistema: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Binding.Should().Be(binding);
    }

    // ─── AdicionarValorDominio (ADR-0116) ───────────────────────────────────

    [Fact(DisplayName = "AdicionarValorDominio em categórico Declarado exige e aceita descrição")]
    public void AdicionarValorDominio_CategoricoDeclarado_Aceita()
    {
        FatoCandidato fato = Criar().Value!;

        Result resultado = fato.AdicionarValorDominio("PRETA", "Autodeclaração de cor/raça preta.", 0, ativo: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.ValoresDominioDeclarados.Should().ContainSingle(v =>
            v.Codigo == "PRETA" && v.Descricao == "Autodeclaração de cor/raça preta." && v.Ordem == 0 && v.Ativo);
    }

    [Fact(DisplayName = "AdicionarValorDominio fora de categórico é rejeitado")]
    public void AdicionarValorDominio_ForaDeCategorico_Falha()
    {
        FatoCandidato fato = Criar(
            codigo: "PCD", dominio: DominioFato.Booleano,
            binding: "CAMPO_FORMULARIO:PCD").Value!;

        Result resultado = fato.AdicionarValorDominio("SIM", "Descrição", 0, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.NaoPermitidoForaDeCategorico);
    }

    [Fact(DisplayName = "AdicionarValorDominio com código duplicado (normalizado, ordinal) é rejeitado")]
    public void AdicionarValorDominio_CodigoDuplicado_Falha()
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio("PRETA", "Descrição", 0, ativo: true).IsSuccess.Should().BeTrue();

        Result resultado = fato.AdicionarValorDominio("  PRETA  ", "Outra descrição", 1, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.CodigoDuplicado);
    }

    [Fact(DisplayName = "AdicionarValorDominio sem descrição quando a origem é Declarado é rejeitado")]
    public void AdicionarValorDominio_SemDescricaoDeclarado_Falha()
    {
        FatoCandidato fato = Criar().Value!;

        Result resultado = fato.AdicionarValorDominio("PRETA", null, 0, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "AdicionarValorDominio sem descrição quando a origem é Derivado é aceito")]
    public void AdicionarValorDominio_SemDescricaoDerivado_Aceita()
    {
        FatoCandidato fato = Criar(
            codigo: "FATO_DERIVADO", dominio: DominioFato.Categorico,
            origem: OrigemFato.Derivado, binding: "REGRA_DERIVACAO:FATO_DERIVADO").Value!;

        Result resultado = fato.AdicionarValorDominio("X", null, 0, ativo: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "AdicionarValorDominio com código em branco é rejeitado")]
    public void AdicionarValorDominio_CodigoEmBranco_Falha()
    {
        FatoCandidato fato = Criar().Value!;

        Result resultado = fato.AdicionarValorDominio("   ", "Descrição", 0, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "AdicionarValorDominio com ordem negativa é rejeitado")]
    public void AdicionarValorDominio_OrdemNegativa_Falha()
    {
        FatoCandidato fato = Criar().Value!;

        Result resultado = fato.AdicionarValorDominio("PRETA", "Descrição", -1, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.OrdemInvalida);
    }

    // ─── Sem remoção lógica (EntityBase puro: o fato é desativado, nunca apagado) ──

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
