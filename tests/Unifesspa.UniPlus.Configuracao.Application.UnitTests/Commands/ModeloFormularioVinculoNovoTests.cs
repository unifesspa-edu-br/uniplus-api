namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A escrita do modelo de formulário recusa só o vínculo novo a fato, termo ou tipo desativado ou
/// pendente, e o que a atualização e a concorrência exigem (ADR-0136).
/// </summary>
public sealed class ModeloFormularioVinculoNovoTests : ModeloFormularioHandlerTestBase
{
    [Fact(DisplayName = "Termo que o catálogo já não traz continua no modelo que o escolheu; o termo novo é recusado")]
    public async Task Termo_RecusaSoOVinculoNovo()
    {
        TermoDoModelo gravado = new("LGPD", 0, Guid.NewGuid(), Guid.NewGuid(), null, Obrigatoriedade.Sempre);
        ItemDoModelo item = new("CERTIFICADO", 0, "DADOS", "Certificado", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false);
        ModeloFormulario existente = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação Medicina", null, FinalidadeFormulario.Habilitacao, null,
            new ConteudoDoModelo("Habilitação", [Secao, Revisao], [item], [gravado], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
        TermoExigidoInput mantido = new(gravado.Codigo, 0, gravado.TermoId, gravado.VersaoId, null, "SEMPRE", null);
        TermoExigidoInput novo = new("IMAGEM", 1, Guid.NewGuid(), Guid.NewGuid(), null, "SEMPRE", null);

        Result editar = await AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)) with { Termos = [mantido, novo] }, tipo: null);

        editar.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.termos[1].versaoId", new DomainError(ModeloFormularioErrorCodes.TermoVersaoNaoEncontrada, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message).Excluding(static e => e.Origem));
    }

    [Fact(DisplayName = "Tipo de processo inexistente ou desativado é recusado; o que o modelo já usava continua na edição")]
    public async Task TipoDeProcesso_RecusaSoONovo()
    {
        Result<Guid> criar = await CriarAsync(Conteudo(Item("CERTIFICADO", 0)), tipo: "PSIQ");
        ModeloFormulario existente = Existente("PSIQ");

        Result editar = await AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)), tipo: "PSIQ");

        criar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TipoProcessoInexistente);
        editar.IsSuccess.Should().BeTrue(editar.Error?.Message);
    }

    [Fact(DisplayName = "Fato sem a proteção aprovada é recusado como vínculo novo; o modelo que já o usava continua com ele")]
    public async Task FatoComProtecaoPendente_RecusaSoOVinculoNovo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        FatoCandidato pendente = Declarado("PENDENTE_DE_APROVACAO");
        Fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [Certificado, pendente]);

        Result<Guid> criar = await CriarAsync(Conteudo(Item("PENDENTE_DE_APROVACAO", 0)));
        Result editar = await AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)), tipo: null);

        criar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoComProtecaoPendente);
        editar.IsSuccess.Should().BeTrue(editar.Error?.Message);
    }

    [Fact(DisplayName = "Fato desativado é recusado como vínculo novo; o modelo que já o usava continua com ele")]
    public async Task FatoDesativado_RecusaSoOVinculoNovo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        Certificado.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> criar = await CriarAsync(Conteudo(Item("CERTIFICADO", 0)));
        Result editar = await AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)), tipo: null);

        criar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
        editar.IsSuccess.Should().BeTrue(editar.Error?.Message);
    }

    [Fact(DisplayName = "Código de fato desativado com espaço é o mesmo fato: o vínculo novo é recusado")]
    public async Task Criar_FatoDesativadoComEspaco_Recusa()
    {
        Certificado.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> resultado = await CriarAsync(Conteudo(Item(" CERTIFICADO", 0)));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "Derivado desativado cujas respostas formam as opções de um item é vínculo novo recusado")]
    public async Task Criar_OpcoesDeDerivadoDesativado_Recusa()
    {
        FatoCandidato opcao = OpcaoComUmValor();
        FatoCandidato perfil = PerfilDerivadoDesativado(opcao);
        Fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([Certificado, opcao, perfil]);
        FatoColetadoInput comOpcoes = Item("OPCAO", 1, "SELECAO_UNICA") with { Restricoes = [new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: ["PERFIL"])] };

        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0), comOpcoes));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "Pressuposto é fato coletável: o calculado pelo sistema é recusado")]
    public async Task Criar_PressupostoNaoColetavel_Recusa()
    {
        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Pressupostos = ["IDADE"] });

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should()
            .Contain(("conteudo.pressupostos[0]", ItemFormularioErrorCodes.FatoNaoColetavel));
    }

    [Fact(DisplayName = "Obrigatoriedade QUANDO sem predicado é recusada na leitura")]
    public async Task Criar_QuandoSemPredicado_Recusa()
    {
        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0) with { Obrigatoriedade = "QUANDO" }));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.itens[0].obrigatoriedade", new DomainError(ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message));
    }

    [Fact(DisplayName = "As recusas do catálogo e as do modelo saem no mesmo lote")]
    public async Task Criar_RecusasDoCatalogoEDoModelo_Acumulam()
    {
        Result<Guid> resultado = await CriarAsync(Conteudo(Item("IDADE", 0, "NUMERO")) with { Titulo = new string('T', 301) });

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [ItemFormularioErrorCodes.FatoNaoColetavel, EstruturaFormularioErrorCodes.TituloTamanho]);
    }

    [Fact(DisplayName = "Conteúdo que não se lê não esconde as recusas do cadastro")]
    public async Task Criar_RecusaDeLeitura_AcumulaAsDoCadastro()
    {
        CriarModeloFormularioCommand command = new(
            "", "Habilitação", null, "XYZ", null, Conteudo(Item("CERTIFICADO", 0) with { Obrigatoriedade = "QUANDO" }));

        Result<Guid> resultado = await CriarModeloFormularioCommandHandler.Handle(
            command, Repository, Fatos, Termos, Tipos, UnitOfWork, CancellationToken.None);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("conteudo.itens[0].obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida),
            ("codigo", ModeloFormularioErrorCodes.CodigoObrigatorio),
            ("finalidade", EstruturaFormularioErrorCodes.FinalidadeInvalida),
        ]);
    }

    [Fact(DisplayName = "O tipo de processo é conferido e guardado na forma em que o cadastro o grava (NFC), ainda que chegue decomposto")]
    public async Task Criar_TipoDecomposto_ConsultaEGuardaONormalizado()
    {
        const string Composto = "ADMISSÃO";
        Tipos.ObterAtivoPorCodigoAsync(Composto, Arg.Any<CancellationToken>()).Returns(new TipoProcessoView(Guid.NewGuid(), Composto, "Admissão", null));

        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0)), tipo: " ADMISSA\u0303O ");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await Repository.Received(1).AdicionarAsync(
            Arg.Is<ModeloFormulario>(m => m.TipoProcessoCodigo == Composto), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida de concorrência (xmin) na gravação descarta o rastreamento e devolve conflito")]
    public async Task Atualizar_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await AtualizarAsync(Existente(tipo: null), Conteudo(Item("CERTIFICADO", 0)), tipo: null);

        resultado.Error!.Code.Should().Be(ModeloFormularioErrorCodes.ConflitoDeConcorrencia);
        UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
    private static FatoCandidato OpcaoComUmValor()
    {
        FatoCandidato opcao = CriacaoDoFato.CriarDoAdministrador(
            "OPCAO", "Opção", "Descrição do fato.", DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global, null, "INSCRICAO",
            EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese).Value!;
        Aprovar(opcao);
        opcao.AdicionarValorDominio("A", "A", 0, ativo: true).IsSuccess.Should().BeTrue();
        return opcao;
    }

    private FatoCandidato PerfilDerivadoDesativado(FatoCandidato opcao)
    {
        FatoCandidato perfil = CriacaoDoFato.CriarDerivadoDoAdministrador(
            "PERFIL", "Perfil", "Descrição do fato.", DominioFato.Categorico, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese).Value!;
        Aprovar(perfil);
        perfil.AdicionarValorDominio("A", "A", 0, ativo: true).IsSuccess.Should().BeTrue();
        RegraDerivacao regra = RegraDerivacao.Criar(
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("CERTIFICADO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            "A").Value!;
        perfil.DefinirRegrasPadrao([regra], new CatalogoDeFatos([Certificado, opcao, perfil], [])).IsSuccess.Should().BeTrue();
        perfil.Desativar().IsSuccess.Should().BeTrue();
        return perfil;
    }

}
