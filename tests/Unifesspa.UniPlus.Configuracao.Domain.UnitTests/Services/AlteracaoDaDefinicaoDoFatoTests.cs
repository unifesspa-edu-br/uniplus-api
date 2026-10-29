namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A alteração dos eixos do fato do administrador (ADR-0136, emenda de 17/10/2026): o subconjunto que
/// o cadastro de cada tipo aceita, as validações do cadastro acumuladas, a coerência com os valores e
/// as dependências do próprio fato, e o fato de sistema fora.
/// </summary>
public sealed class AlteracaoDaDefinicaoDoFatoTests
{
    private const string Finalidade = "Enquadramento na reserva de vagas.";
    private const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;
    private static readonly PrecedenciaFase[] InscricaoAntesDeHabilitacao =
        [PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!];

    [Fact(DisplayName = "Fato declarado troca domínio, fonte, escopo, ponto de resolução e proteção; código, origem e vínculo ficam")]
    public void Declarado_TrocaOsEixos()
    {
        FatoCandidato fato = Declarado("TEM_RENDA");

        Result resultado = fato.AlterarDefinicao(
            Definicao(DominioFato.Categorico, CardinalidadeFato.Multivalorado, FonteValoresFato.Global, ponto: "HABILITACAO",
                escopo: EscopoFato.MembroGrupo, classificacao: ClassificacaoDeProtecao.Sensivel, finalidade: " Nova finalidade "),
            Catalogo(fato));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.Should().BeEquivalentTo(new
        {
            Codigo = "TEM_RENDA",
            Origem = OrigemFato.Declarado,
            Binding = "CAMPO_FORMULARIO:TEM_RENDA",
            Dominio = DominioFato.Categorico,
            Cardinalidade = CardinalidadeFato.Multivalorado,
            FonteValores = FonteValoresFato.Global,
            PontoResolucao = "HABILITACAO",
            Escopo = EscopoFato.MembroGrupo,
            ClassificacaoProtecao = ClassificacaoDeProtecao.Sensivel,
            FinalidadeTratamento = "Nova finalidade",
        });
    }

    [Fact(DisplayName = "Derivado por regra troca o domínio e infere a cardinalidade e a fonte dele")]
    public void Derivado_TrocaODominio()
    {
        FatoCandidato fato = Derivado("PERFIL");

        Result resultado = fato.AlterarDefinicao(
            Definicao(DominioFato.Categorico, ponto: "HABILITACAO", escopo: EscopoFato.Candidato), Catalogo(fato));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.Should().BeEquivalentTo(new
        {
            Dominio = DominioFato.Categorico,
            Cardinalidade = CardinalidadeFato.Multivalorado,
            FonteValores = FonteValoresFato.Global,
            PontoResolucao = "HABILITACAO",
            Binding = "REGRA_DERIVACAO:PERFIL",
        });
    }

    [Fact(DisplayName = "Agregado troca só o ponto de resolução e a proteção; domínio, fonte e escopo continuam os do membro")]
    public void Agregado_TrocaPontoEProtecao()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);

        Result resultado = agregado.AlterarDefinicao(
            Definicao(dominio: null, ponto: "HABILITACAO", classificacao: ClassificacaoDeProtecao.Sensivel, escopo: null), Catalogo(membro, agregado));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        agregado.Should().BeEquivalentTo(new
        {
            Dominio = DominioFato.Categorico,
            Cardinalidade = CardinalidadeFato.Multivalorado,
            FonteValores = FonteValoresFato.Global,
            Escopo = EscopoFato.Candidato,
            PontoResolucao = "HABILITACAO",
            ClassificacaoProtecao = ClassificacaoDeProtecao.Sensivel,
            Binding = "AGREGACAO_GRUPO:CATEGORIA_RENDA",
        });
    }

    [Fact(DisplayName = "Fato de sistema recusa a alteração dos eixos")]
    public void Sistema_Recusa()
    {
        FatoCandidato sistema = FatoCandidato.Criar(
            "COR_RACA", "Cor ou raça", null, DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            null, "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", EscopoFato.Candidato, ClassificacaoDeProtecao.Sensivel, Finalidade, Hipotese,
            sistema: true).Value!;

        sistema.AlterarDefinicao(Definicao(DominioFato.Booleano), Catalogo(sistema))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.FatoDeSistemaSoEditaNomeEDescricao);
    }

    [Fact(DisplayName = "As recusas do cadastro acumulam, cada uma no campo do eixo, e o fato fica como estava")]
    public void Declarado_RecusasDoCadastro_AcumulamESemMutar()
    {
        FatoCandidato fato = Declarado("TEM_RENDA");

        Result resultado = fato.AlterarDefinicao(
            new DefinicaoDoFato(
                DominioFato.Texto, CardinalidadeFato.Escalar, FonteValoresFato.Global, FormatoTexto.Nenhum, "INSCRICAO", EscopoFato.Candidato,
                ClassificacaoDeProtecao.Publico, Finalidade, HipoteseLegalTratamento.Consentimento),
            Catalogo(fato));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("fonteValores", FatoCandidatoErrorCodes.FonteValoresForaDeCategorico),
            ("formato", FatoCandidatoErrorCodes.FormatoObrigatorio),
            ("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio),
        ]);
        fato.Should().BeEquivalentTo(new { Dominio = DominioFato.Booleano, FonteValores = (FonteValoresFato?)null, ClassificacaoProtecao = ClassificacaoDeProtecao.Pessoal });
    }

    [Fact(DisplayName = "Hipótese legal que não cabe na classificação e formato fora de texto são recusados")]
    public void Declarado_HipoteseEFormato_Recusa()
    {
        FatoCandidato fato = Declarado("TEM_RENDA");

        Result resultado = fato.AlterarDefinicao(
            DeDeclarado(DominioFato.Booleano, FormatoTexto.Cpf, ClassificacaoDeProtecao.Sensivel, HipoteseLegalTratamento.ExecucaoContrato),
            Catalogo(fato));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("formato", FatoCandidatoErrorCodes.FormatoForaDeTexto),
            ("hipoteseLegal", FatoCandidatoErrorCodes.HipoteseLegalIncompativelComClassificacao),
        ]);
    }

    [Fact(DisplayName = "Derivado por regra de domínio texto é recusado no domínio")]
    public void Derivado_Texto_Recusa()
    {
        FatoCandidato fato = Derivado("PERFIL");

        fato.AlterarDefinicao(Definicao(DominioFato.Texto, classificacao: ClassificacaoDeProtecao.Pessoal), Catalogo(fato))
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "dominio",
                Error = new { Code = FatoCandidatoErrorCodes.DerivadoPorRegraSoBooleanoOuCategorico },
            });
    }

    [Fact(DisplayName = "O eixo que o tipo do fato infere não se informa")]
    public void EixoInferido_Informado_Recusa()
    {
        FatoCandidato derivado = Derivado("PERFIL");
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);

        derivado.AlterarDefinicao(Definicao(DominioFato.Booleano, cardinalidade: CardinalidadeFato.Multivalorado), Catalogo(derivado))
            .Errors.Should().ContainSingle().Which.Field.Should().Be("cardinalidade");
        agregado.AlterarDefinicao(Definicao(DominioFato.Categorico, escopo: EscopoFato.MembroGrupo), Catalogo(membro, agregado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [
                ("dominio", FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato),
                ("escopo", FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato),
            ]);
    }

    [Fact(DisplayName = "Domínio ou fonte que não comporta os valores cadastrados é recusado no campo do eixo")]
    public void Declarado_ComValores_RecusaDominioEFonteQueNaoOsComportam()
    {
        FatoCandidato fato = Declarado("TIPO_RENDA", DominioFato.Categorico, FonteValoresFato.Global);
        fato.AdicionarValorDominio("RURAL", "Rural", 0, ativo: true).IsSuccess.Should().BeTrue();
        fato.DesativarValor("RURAL").IsSuccess.Should().BeTrue();

        fato.AlterarDefinicao(DeDeclarado(DominioFato.Booleano), Catalogo(fato))
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "dominio",
                Error = new { Code = FatoCandidatoErrorCodes.DominioNaoComportaOsValores },
            }, "o valor desativado continua no fato, porque o código nunca é removido");
        fato.AlterarDefinicao(Definicao(DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Processo), Catalogo(fato))
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "fonteValores",
                Error = new { Code = FatoCandidatoErrorCodes.FonteNaoComportaOsValores },
            });
    }

    [Fact(DisplayName = "Domínio que deixa de valer as regras padrão do derivado é recusado no domínio")]
    public void Derivado_RegrasPadrao_RecusaDominioQueDeixaDeValer()
    {
        FatoCandidato renda = Declarado("TEM_RENDA");
        FatoCandidato derivado = Derivado("PERFIL");
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], Catalogo(renda, derivado)).IsSuccess.Should().BeTrue();

        Result resultado = derivado.AlterarDefinicao(Definicao(DominioFato.Categorico), Catalogo(renda, derivado));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("dominio");
        derivado.Dominio.Should().Be(DominioFato.Booleano);
    }

    [Fact(DisplayName = "Classificação mais fraca que a de uma dependência é recusada na classificação, no derivado e no agregado")]
    public void ClassificacaoAbaixoDaDependencia_Recusa()
    {
        FatoCandidato renda = Declarado("TEM_RENDA", classificacao: ClassificacaoDeProtecao.Pessoal);
        FatoCandidato derivado = Derivado("PERFIL", classificacao: ClassificacaoDeProtecao.Pessoal);
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], Catalogo(renda, derivado)).IsSuccess.Should().BeTrue();
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", classificacao: ClassificacaoDeProtecao.Sensivel);
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro, ClassificacaoDeProtecao.Sensivel);

        derivado.AlterarDefinicao(Definicao(DominioFato.Booleano, classificacao: ClassificacaoDeProtecao.Publico), Catalogo(renda, derivado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia));
        agregado.AlterarDefinicao(Definicao(dominio: null, escopo: null, classificacao: ClassificacaoDeProtecao.Pessoal), Catalogo(membro, agregado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia));
    }

    [Fact(DisplayName = "Ponto de resolução anterior ao de uma dependência é recusado no ponto, no derivado e no agregado")]
    public void PontoAnteriorADependencia_Recusa()
    {
        FatoCandidato renda = Declarado("TEM_RENDA", ponto: "HABILITACAO");
        FatoCandidato derivado = Derivado("PERFIL", ponto: "HABILITACAO");
        CatalogoDeFatos catalogoDoDerivado = new([renda, derivado], InscricaoAntesDeHabilitacao);
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], catalogoDoDerivado).IsSuccess.Should().BeTrue();
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", ponto: "HABILITACAO");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro, ponto: "HABILITACAO", precedencias: InscricaoAntesDeHabilitacao);

        derivado.AlterarDefinicao(Definicao(DominioFato.Booleano, ponto: "INSCRICAO"), catalogoDoDerivado)
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("pontoResolucao", FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia));
        agregado.AlterarDefinicao(
                Definicao(dominio: null, escopo: null, ponto: "INSCRICAO"), new CatalogoDeFatos([membro, agregado], InscricaoAntesDeHabilitacao))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("pontoResolucao", FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia));
    }

    [Fact(DisplayName = "O agregado deixa de valer quando o membro troca de domínio, de fonte ou de escopo")]
    public void MotivosPelosQuaisDeixaDeAgregar_MembroMudou()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);
        FatoCandidato comOutraFonte = DeMembro("CATEGORIA_RENDA", fonte: FonteValoresFato.Processo);
        FatoCandidato booleano = DeMembro("CATEGORIA_RENDA", DominioFato.Booleano);
        FatoCandidato foraDeGrupo = Declarado("CATEGORIA_RENDA", DominioFato.Categorico, FonteValoresFato.Global);
        CatalogoDeFatos catalogo = Catalogo(membro, agregado);

        agregado.MotivosPelosQuaisDeixaDeAgregar(membro, catalogo).Should().BeEmpty();
        agregado.MotivosPelosQuaisDeixaDeAgregar(comOutraFonte, catalogo).Should().ContainSingle().Which.Error.Message.Should().Contain("fonte");
        agregado.MotivosPelosQuaisDeixaDeAgregar(booleano, catalogo).Should().ContainSingle().Which.Error.Message.Should().Contain("domínio");
        agregado.MotivosPelosQuaisDeixaDeAgregar(foraDeGrupo, catalogo).Should().ContainSingle().Which.Error.Message.Should().Contain("membro de grupo");
    }

    [Fact(DisplayName = "Derivado e agregado inferem os eixos da mesma regra do cadastro, na alteração e na conferência")]
    public void EixosInferidos_SeguemOCadastro()
    {
        FatoCandidato derivado = Derivado("PERFIL");
        derivado.AlterarDefinicao(Definicao(DominioFato.Categorico), Catalogo(derivado)).IsSuccess.Should().BeTrue();
        FatoCandidato cadastrado = Derivado("OUTRO", DominioFato.Categorico);

        derivado.Should().BeEquivalentTo(new { cadastrado.Cardinalidade, cadastrado.FonteValores });

        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);
        agregado.MotivosPelosQuaisDeixaDeAgregar(membro, Catalogo(membro, agregado)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Domínio fora do vocabulário é recusado como inválido, sem a recusa de proteção mínima sobre ele")]
    public void DominioForaDoVocabulario_RecusaComoInvalidoNoCampo()
    {
        FatoCandidato derivado = Derivado("PERFIL");
        FatoCandidato declarado = Declarado("TEM_RENDA");
        const DominioFato foraDoVocabulario = (DominioFato)(-1);

        derivado.AlterarDefinicao(Definicao(foraDoVocabulario, classificacao: ClassificacaoDeProtecao.Publico), Catalogo(derivado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("dominio", FatoCandidatoErrorCodes.DominioInvalido));
        derivado.AlterarDefinicao(Definicao(DominioFato.Texto, classificacao: ClassificacaoDeProtecao.Publico), Catalogo(derivado))
            .Errors.Select(static e => e.Field).Should().Equal("dominio");
        declarado.AlterarDefinicao(Definicao(foraDoVocabulario, CardinalidadeFato.Escalar, classificacao: ClassificacaoDeProtecao.Publico), Catalogo(declarado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("dominio", FatoCandidatoErrorCodes.DominioInvalido));
    }

    [Fact(DisplayName = "Recusa que já existia antes da alteração não a trava; a que a alteração causa, sim")]
    public void RecusaPreexistente_NaoBloqueia_RecusaNova_Bloqueia()
    {
        // A precedência passou a pôr a inscrição antes da habilitação depois que a regra foi aceita.
        FatoCandidato renda = Declarado("TEM_RENDA", ponto: "HABILITACAO");
        FatoCandidato derivado = Derivado("PERFIL", ponto: "INSCRICAO");
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], Catalogo(renda, derivado)).IsSuccess.Should().BeTrue();
        CatalogoDeFatos comPrecedencia = new([renda, derivado], InscricaoAntesDeHabilitacao);

        derivado.AlterarDefinicao(Definicao(DominioFato.Booleano, ponto: "INSCRICAO", finalidade: "Outra finalidade."), comPrecedencia)
            .IsSuccess.Should().BeTrue("a recusa de ponto já existia e a alteração não a causa");

        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", ponto: "HABILITACAO");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro, ponto: "HABILITACAO", precedencias: InscricaoAntesDeHabilitacao);
        CatalogoDeFatos catalogoDoAgregado = new([membro, agregado], InscricaoAntesDeHabilitacao);
        agregado.AlterarDefinicao(Definicao(dominio: null, escopo: null, ponto: "HABILITACAO", finalidade: "Outra finalidade."), catalogoDoAgregado)
            .IsSuccess.Should().BeTrue();
        agregado.AlterarDefinicao(Definicao(dominio: null, escopo: null, ponto: "INSCRICAO"), catalogoDoAgregado)
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia);
    }

    [Fact(DisplayName = "Agregado deixa de valer com o membro desativado ou com a cardinalidade fora da inferida")]
    public void MotivosPelosQuaisDeixaDeAgregar_MembroInativoECardinalidade()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);
        CatalogoDeFatos catalogo = Catalogo(membro, agregado);
        FatoCandidato inativo = DeMembro("CATEGORIA_RENDA");
        inativo.Desativar().IsSuccess.Should().BeTrue();

        agregado.MotivosPelosQuaisDeixaDeAgregar(inativo, catalogo).Should().ContainSingle().Which.Error.Message.Should().Contain("desativado");

        FatoCandidato booleanoEscalar = Agregado("TEM_ALGUM", DeMembro("FLAG", DominioFato.Booleano));
        FatoCandidato membroBooleano = DeMembro("FLAG", DominioFato.Booleano);
        booleanoEscalar.AlterarDefinicao(Definicao(dominio: null, escopo: null), Catalogo(membroBooleano, booleanoEscalar)).IsSuccess.Should().BeTrue();
        typeof(FatoCandidato).GetProperty(nameof(FatoCandidato.Cardinalidade))!.SetValue(booleanoEscalar, CardinalidadeFato.Multivalorado);
        booleanoEscalar.MotivosPelosQuaisDeixaDeAgregar(membroBooleano, Catalogo(membroBooleano, booleanoEscalar))
            .Should().ContainSingle().Which.Error.Message.Should().Contain("cardinalidade");
    }

    [Fact(DisplayName = "A recusa preexistente cuja mensagem muda não conta como nova; o motivo do agregado tem identidade estável")]
    public void DiferencaDeRecusas_ComparaPorCampoECodigo_NaoPelaMensagem()
    {
        FieldError antes = new("pontoResolucao", new DomainError("X.Ponto", "resolve em INSCRICAO"), "regras[0].A");
        FieldError mesmaComOutroTexto = new("pontoResolucao", new DomainError("X.Ponto", "resolve em HABILITACAO"), "regras[0].A");
        FieldError outroCodigo = new("pontoResolucao", new DomainError("X.Outro", "resolve em INSCRICAO"), "regras[0].A");
        FieldError outroFatoCitado = new("pontoResolucao", new DomainError("X.Ponto", "resolve em INSCRICAO"), "regras[0].B");
        FieldError outraRegra = new("pontoResolucao", new DomainError("X.Ponto", "resolve em INSCRICAO"), "regras[1].A");

        DiferencaDeRecusas.Novas([mesmaComOutroTexto, outroCodigo, outroFatoCitado, outraRegra], [antes])
            .Should().Equal(outroCodigo, outroFatoCitado, outraRegra);

        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);
        FatoCandidato inativo = DeMembro("CATEGORIA_RENDA");
        inativo.Desativar().IsSuccess.Should().BeTrue();
        FieldError motivo = agregado.MotivosPelosQuaisDeixaDeAgregar(inativo, Catalogo(membro, agregado)).Should().ContainSingle().Subject;
        motivo.Error.Code.Should().Be(FatoCandidatoErrorCodes.AlteracaoQuebraAgregadoDoFato);
        motivo.Field.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Domínio omitido no derivado é recusado como obrigatório")]
    public void DerivadoSemDominio_RecusaComoObrigatorio()
    {
        FatoCandidato derivado = Derivado("PERFIL");

        derivado.AlterarDefinicao(Definicao(dominio: null), Catalogo(derivado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("dominio", FatoCandidatoErrorCodes.DominioObrigatorio));
    }

    [Fact(DisplayName = "Regra padrão de outro derivado que já tinha recusa contra uma dependência recusa a alteração que causa a mesma contra outra")]
    public void RegraPadraoDeOutroFato_RecusaContraOutraDependenciaNaMesmaRegra()
    {
        FatoCandidato a = Declarado("FATO_A");
        FatoCandidato b = Declarado("FATO_B");
        FatoCandidato derivado = Derivado("PERFIL");
        derivado.DefinirRegrasPadrao([Booleana(("FATO_A", true), ("FATO_B", true))], Catalogo(a, b, derivado)).IsSuccess.Should().BeTrue();
        CatalogoDeFatos catalogo = Catalogo(a, b, derivado);
        a.AlterarDefinicao(DeDeclarado(DominioFato.Booleano, classificacao: ClassificacaoDeProtecao.Sensivel), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> antes = [.. ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo).Errors];

        b.AlterarDefinicao(DeDeclarado(DominioFato.Booleano, classificacao: ClassificacaoDeProtecao.Sensivel), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> depois = [.. ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo).Errors];

        DiferencaDeRecusas.Novas(depois, antes).Should().ContainSingle()
            .Which.Should().Match<FieldError>(static e =>
                e.Error.Code == FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia && e.Origem == "regras[0].FATO_B");
    }

    [Fact(DisplayName = "Regra padrão de outro derivado cuja recusa contra a mesma dependência só muda de texto não recusa a alteração")]
    public void RegraPadraoDeOutroFato_MesmaRecusaComOutroTexto_NaoRecusa()
    {
        FatoCandidato a = Declarado("FATO_A");
        FatoCandidato derivado = Derivado("PERFIL");
        derivado.DefinirRegrasPadrao([Booleana(("FATO_A", true))], Catalogo(a, derivado)).IsSuccess.Should().BeTrue();
        PrecedenciaFase[] precedencias =
        [
            PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!,
            PrecedenciaFase.Criar("INSCRICAO", "RESULTADO_FINAL", permiteSobreposicao: false, []).Value!,
        ];
        CatalogoDeFatos catalogo = new([a, derivado], precedencias);
        a.AlterarDefinicao(DeDeclarado(DominioFato.Booleano, ponto: "HABILITACAO"), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> antes = [.. ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo).Errors];

        a.AlterarDefinicao(DeDeclarado(DominioFato.Booleano, ponto: "RESULTADO_FINAL"), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> depois = [.. ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo).Errors];

        antes.Should().ContainSingle();
        depois.Should().ContainSingle().Which.Error.Message.Should().NotBe(antes[0].Error.Message);
        DiferencaDeRecusas.Novas(depois, antes).Should().BeEmpty();
    }

    [Fact(DisplayName = "Regra de aplicabilidade que já tinha recusa contra um fato recusa a alteração que causa a mesma contra outro")]
    public void RegraDeAplicabilidade_RecusaContraOutroFatoCitado()
    {
        FatoCandidato a = Declarado("FATO_A", ponto: "HABILITACAO");
        FatoCandidato b = Declarado("FATO_B");
        CatalogoDeFatos catalogo = Catalogo(a, b);
        PredicadoDnf regra = Quando(("FATO_A", true), ("FATO_B", true));
        List<FieldError> antes = [.. ConferenciaDaRegraDeAplicabilidade.Conferir(regra, regra, catalogo)];

        b.AlterarDefinicao(DeDeclarado(DominioFato.Booleano, ponto: "HABILITACAO"), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> depois = [.. ConferenciaDaRegraDeAplicabilidade.Conferir(regra, regra, catalogo)];

        antes.Should().ContainSingle();
        DiferencaDeRecusas.Novas(depois, antes).Should().ContainSingle()
            .Which.Should().Match<FieldError>(static e => e.Origem == "FATO_B");
    }

    [Fact(DisplayName = "Regra de aplicabilidade cuja recusa contra o mesmo fato só muda de texto não recusa a alteração")]
    public void RegraDeAplicabilidade_MesmaRecusaComOutroTexto_NaoRecusa()
    {
        FatoCandidato a = Declarado("FATO_A", ponto: "HABILITACAO");
        CatalogoDeFatos catalogo = Catalogo(a);
        PredicadoDnf regra = Quando(("FATO_A", true));
        List<FieldError> antes = [.. ConferenciaDaRegraDeAplicabilidade.Conferir(regra, regra, catalogo)];

        a.AlterarDefinicao(DeDeclarado(DominioFato.Booleano, ponto: "RESULTADO_FINAL"), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> depois = [.. ConferenciaDaRegraDeAplicabilidade.Conferir(regra, regra, catalogo)];

        depois.Should().ContainSingle().Which.Error.Message.Should().NotBe(antes.Should().ContainSingle().Subject.Error.Message);
        DiferencaDeRecusas.Novas(depois, antes).Should().BeEmpty();
    }

    [Fact(DisplayName = "Regras do próprio derivado: a regra que passa a resolver antes de outra dependência recusa mesmo com recusa antiga em outra regra")]
    public void RegrasDoProprioDerivado_RecusaARegraNovaMesmoComOutraJaRecusada()
    {
        (FatoCandidato derivado, CatalogoDeFatos catalogo) = DerivadoComDuasRegras();
        // Em HABILITACAO o derivado só resolve antes de A (RESULTADO_FINAL); antecipado para INSCRICAO, também de B.

        Result resultado = derivado.AlterarDefinicao(Definicao(ponto: "INSCRICAO"), catalogo);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "pontoResolucao"
            && e.Error.Code == FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia
            && e.Origem == "regras[1].FATO_B");
    }

    [Fact(DisplayName = "Regras do próprio derivado: a recusa que só muda de texto contra a mesma dependência não recusa a alteração")]
    public void RegrasDoProprioDerivado_MesmaRecusaComOutroTexto_NaoRecusa()
    {
        (FatoCandidato derivado, CatalogoDeFatos catalogo) = DerivadoComDuasRegras(segundaRegra: false);

        derivado.AlterarDefinicao(Definicao(ponto: "INSCRICAO"), catalogo).IsSuccess.Should().BeTrue();
    }

    // O derivado resolve em HABILITACAO, antes de A (RESULTADO_FINAL, recusa já existente); a segunda
    // regra cita B, que só é conhecido em HABILITACAO.
    private static (FatoCandidato Derivado, CatalogoDeFatos Catalogo) DerivadoComDuasRegras(bool segundaRegra = true)
    {
        FatoCandidato a = Declarado("FATO_A");
        FatoCandidato b = Declarado("FATO_B", ponto: "HABILITACAO");
        FatoCandidato derivado = Derivado("PERFIL", ponto: "HABILITACAO");
        RegraDerivacao[] regras = segundaRegra ? [Booleana(("FATO_A", true)), Booleana(("FATO_B", true))] : [Booleana(("FATO_A", true))];
        derivado.DefinirRegrasPadrao(regras, Catalogo(a, b, derivado)).IsSuccess.Should().BeTrue();
        a.AlterarDefinicao(DeDeclarado(DominioFato.Booleano, ponto: "RESULTADO_FINAL"), Catalogo(a, b, derivado)).IsSuccess.Should().BeTrue();
        PrecedenciaFase[] precedencias =
        [
            PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!,
            PrecedenciaFase.Criar("HABILITACAO", "RESULTADO_FINAL", permiteSobreposicao: false, []).Value!,
        ];
        return (derivado, new CatalogoDeFatos([a, b, derivado], precedencias));
    }

    [Fact(DisplayName = "Classificação e hipótese legal fora do vocabulário são recusadas como fora do vocabulário, não como ausentes")]
    public void ProtecaoForaDoVocabulario_RecusaComoForaDoVocabulario()
    {
        FatoCandidato declarado = Declarado("TEM_RENDA");

        declarado.AlterarDefinicao(
                DeDeclarado(DominioFato.Booleano, classificacao: (ClassificacaoDeProtecao)(-1), hipotese: (HipoteseLegalTratamento)(-1)),
                Catalogo(declarado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [
                ("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoProtecaoInvalida),
                ("hipoteseLegal", FatoCandidatoErrorCodes.HipoteseLegalInvalida),
            ]);
    }

    private static DefinicaoDoFato Definicao(
        DominioFato? dominio = DominioFato.Booleano,
        CardinalidadeFato? cardinalidade = null,
        FonteValoresFato? fonte = null,
        FormatoTexto? formato = null,
        string ponto = "INSCRICAO",
        EscopoFato? escopo = EscopoFato.Candidato,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        string finalidade = Finalidade,
        HipoteseLegalTratamento hipotese = Hipotese) =>
        new(dominio, cardinalidade, fonte, formato, ponto, escopo, classificacao, finalidade, hipotese);

    // O declarado informa a cardinalidade; o derivado e o agregado a inferem.
    private static DefinicaoDoFato DeDeclarado(
        DominioFato dominio,
        FormatoTexto? formato = null,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        HipoteseLegalTratamento hipotese = Hipotese,
        string ponto = "INSCRICAO") =>
        Definicao(dominio, CardinalidadeFato.Escalar, formato: formato, ponto: ponto, classificacao: classificacao, hipotese: hipotese);

    private static FatoCandidato Declarado(
        string codigo,
        DominioFato dominio = DominioFato.Booleano,
        FonteValoresFato? fonte = null,
        string ponto = "INSCRICAO",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal) =>
        FatoCandidato.CriarDoAdministrador(
            codigo, codigo, null, dominio, CardinalidadeFato.Escalar, fonte, null, ponto, EscopoFato.Candidato,
            classificacao, Finalidade, Hipotese).Value!;

    private static FatoCandidato DeMembro(
        string codigo,
        DominioFato dominio = DominioFato.Categorico,
        FonteValoresFato fonte = FonteValoresFato.Global,
        string ponto = "INSCRICAO",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal) =>
        FatoCandidato.CriarDoAdministrador(
            codigo, codigo, null, dominio, CardinalidadeFato.Escalar, dominio == DominioFato.Categorico ? fonte : null, null, ponto,
            EscopoFato.MembroGrupo, classificacao, Finalidade, Hipotese).Value!;

    private static FatoCandidato Agregado(
        string codigo,
        FatoCandidato membro,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        string ponto = "INSCRICAO",
        PrecedenciaFase[]? precedencias = null) =>
        FatoCandidato.CriarAgregadoDoAdministrador(
            codigo, codigo, null, membro.Codigo, new CatalogoDeFatos([membro], precedencias ?? []), ponto, classificacao, Finalidade, Hipotese).Value!;

    private static FatoCandidato Derivado(
        string codigo,
        DominioFato dominio = DominioFato.Booleano,
        string ponto = "INSCRICAO",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal) =>
        FatoCandidato.CriarDerivadoDoAdministrador(
            codigo, codigo, null, dominio, ponto, EscopoFato.Candidato, classificacao, Finalidade, Hipotese).Value!;

    private static CatalogoDeFatos Catalogo(params FatoCandidato[] fatos) => new(fatos, []);

    private static RegraDerivacao Booleana(params (string Fato, object Valor)[] condicoes) =>
        RegraDerivacao.CriarBooleana(Quando(condicoes));

    private static PredicadoDnf Quando(params (string Fato, object Valor)[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. condicoes.Select(static c => (0, CondicaoDnf.Criar(c.Fato, Operador.Igual, JsonSerializer.SerializeToElement(c.Valor)).Value!))]).Value!;
}
