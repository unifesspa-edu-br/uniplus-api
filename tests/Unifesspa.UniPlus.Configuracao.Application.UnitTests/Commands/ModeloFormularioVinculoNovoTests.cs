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
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>
/// A escrita do modelo de formulário recusa só o vínculo novo a fato, termo ou tipo desativado ou
/// pendente, e o que a atualização e a concorrência exigem (ADR-0136).
/// </summary>
public sealed class ModeloFormularioVinculoNovoTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    private static ModeloFormulario ExistenteComOTermo(TermoDoModelo termo)
    {
        ItemDoModelo item = new(
            "CERTIFICADO", 0, "DADOS", new ApresentacaoDoCampo("Certificado", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []));
        return ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação Medicina", null, DeOutraFaseObrigatoria, null,
            new ConteudoDoModelo("Habilitação", [Secao, Revisao], [item], [termo], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
    }

    [Fact(DisplayName = "Termo que o catálogo já não traz continua no modelo que o escolheu; o termo novo é recusado")]
    public async Task Termo_RecusaSoOVinculoNovo()
    {
        TermoDoModelo gravado = new("LGPD", 0, Guid.NewGuid(), Guid.NewGuid(), new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre));
        ModeloFormulario existente = ExistenteComOTermo(gravado);
        TermoExigidoInput mantido = EntradaDoTermoExigidoDeTeste.Exigindo(gravado.Codigo).DaVersao(gravado.TermoId, gravado.VersaoId).Construir();
        TermoExigidoInput novo = EntradaDoTermoExigidoDeTeste.Exigindo("IMAGEM", 1).DaVersao(Guid.NewGuid(), Guid.NewGuid()).Construir();

        Result editar = await _cenario.AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)) with { Termos = [mantido, novo] }, tipo: null);

        editar.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.termos[1].termo.versaoId", new DomainError(ModeloFormularioErrorCodes.TermoVersaoNaoEncontrada, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message).Excluding(static e => e.Origem));
    }

    [Fact(DisplayName = "Tipo de processo inexistente ou desativado é recusado; o que o modelo já usava continua na edição")]
    public async Task TipoDeProcesso_RecusaSoONovo()
    {
        Result<Guid> criar = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)), tipo: "INDIGENA_E_QUILOMBOLA");
        ModeloFormulario existente = Existente("INDIGENA_E_QUILOMBOLA");

        Result editar = await _cenario.AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)), tipo: "INDIGENA_E_QUILOMBOLA");

        criar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TipoProcessoInexistente);
        editar.IsSuccess.Should().BeTrue(editar.Error?.Message);
    }

    [Fact(DisplayName = "Fato sem a proteção aprovada é recusado como vínculo novo; o modelo que já o usava continua com ele")]
    public async Task FatoComProtecaoPendente_RecusaSoOVinculoNovo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        FatoCandidato pendente = Declarado("PENDENTE_DE_APROVACAO");
        _cenario.Fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [_cenario.Certificado, pendente]);

        Result<Guid> criar = await _cenario.CriarAsync(Conteudo(Item("PENDENTE_DE_APROVACAO", 0)));
        Result editar = await _cenario.AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)), tipo: null);

        criar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoComProtecaoPendente);
        editar.IsSuccess.Should().BeTrue(editar.Error?.Message);
    }

    [Fact(DisplayName = "Fato desativado é recusado como vínculo novo; o modelo que já o usava continua com ele")]
    public async Task FatoDesativado_RecusaSoOVinculoNovo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        _cenario.Certificado.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> criar = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)));
        Result editar = await _cenario.AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)), tipo: null);

        criar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
        editar.IsSuccess.Should().BeTrue(editar.Error?.Message);
    }

    [Fact(DisplayName = "Código de fato com espaço não existe no catálogo")]
    public async Task Criar_FatoComEspaco_RecusaComoDesconhecido()
    {
        _cenario.Certificado.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item(" CERTIFICADO", 0)));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.FatoDesconhecido);
    }

    [Fact(DisplayName = "Derivado desativado cujas respostas formam as opções de um item é vínculo novo recusado")]
    public async Task Criar_OpcoesDeDerivadoDesativado_Recusa()
    {
        FatoCandidato opcao = OpcaoComUmValor();
        FatoCandidato perfil = PerfilDerivadoDesativado(opcao);
        _cenario.Fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([_cenario.Certificado, opcao, perfil]);
        FatoColetadoInput comOpcoes = Montar("OPCAO", 1, "ESCOLHA_UNICA").ComRestricoes(new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: ["PERFIL"])).Construir();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0), comOpcoes));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "Pressuposto é fato coletável: o calculado pelo sistema é recusado")]
    public async Task Criar_PressupostoNaoColetavel_Recusa()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Pressupostos = ["IDADE"] });

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should()
            .Contain(("conteudo.pressupostos[0]", ItemFormularioErrorCodes.FatoNaoColetavel));
    }

    [Fact(DisplayName = "Obrigatoriedade QUANDO sem predicado é recusada na leitura")]
    public async Task Criar_QuandoSemPredicado_Recusa()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Montar("CERTIFICADO", 0).ComObrigatoriedade("QUANDO").Construir()));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.itens[0].regras.obrigatoriedade", new DomainError(ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message));
    }

    [Fact(DisplayName = "As recusas do catálogo e as do modelo saem no mesmo lote")]
    public async Task Criar_RecusasDoCatalogoEDoModelo_Acumulam()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("IDADE", 0, "NUMERO")) with { Titulo = new string('T', 301) });

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [ItemFormularioErrorCodes.FatoNaoColetavel, EstruturaFormularioErrorCodes.TituloTamanho]);
    }

    [Fact(DisplayName = "Conteúdo que não se lê não esconde as recusas do cadastro")]
    public async Task Criar_RecusaDeLeitura_AcumulaAsDoCadastro()
    {
        CriarModeloFormularioCommand command = new(
            "", null, new DescritivoDoModeloInput("Habilitação", null, null), Conteudo(Montar("CERTIFICADO", 0).ComObrigatoriedade("QUANDO").Construir()));

        Result<Guid> resultado = await CriarModeloFormularioCommandHandler.Handle(
            command, _cenario.Repository, _cenario.Fatos, _cenario.Termos, _cenario.Tipos, _cenario.UnitOfWork, CancellationToken.None);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("conteudo.itens[0].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida),
            ("codigo", ModeloFormularioErrorCodes.CodigoObrigatorio),
            ("declaracao", ModeloFormularioErrorCodes.DeclaracaoIncompleta),
        ]);
    }

    [Fact(DisplayName = "O tipo de processo válido é conferido e guardado como recebido")]
    public async Task Criar_TipoValido_ConsultaEGuardaORecebido()
    {
        const string Codigo = "ADMISSAO";
        _cenario.Tipos.ObterAtivoPorCodigoAsync(Codigo, Arg.Any<CancellationToken>()).Returns(new TipoProcessoView(Guid.NewGuid(), Codigo, "Admissão", null));

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)), tipo: Codigo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await _cenario.Repository.Received(1).AdicionarAsync(
            Arg.Is<ModeloFormulario>(m => m.TipoProcessoCodigo == Codigo), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida de concorrência (xmin) na gravação descarta o rastreamento e devolve conflito")]
    public async Task Atualizar_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await _cenario.AtualizarAsync(Existente(tipo: null), Conteudo(Item("CERTIFICADO", 0)), tipo: null);

        resultado.Error!.Code.Should().Be(ModeloFormularioErrorCodes.ConflitoDeConcorrencia);
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
    private static FatoCandidato OpcaoComUmValor()
    {
        FatoCandidato opcao = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato("OPCAO", "Opção", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, FonteValoresFato.Catalogo, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;
        Aprovar(opcao);
        opcao.AdicionarValorDominio("A", "A", 0, ativo: true).IsSuccess.Should().BeTrue();
        return opcao;
    }

    private FatoCandidato PerfilDerivadoDesativado(FatoCandidato opcao)
    {
        FatoCandidato perfil = CriacaoDoFato.CriarDerivadoDoAdministrador(
            new DescritivoDoFato("PERFIL", "Perfil", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.Opcao, null, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;
        Aprovar(perfil);
        perfil.AdicionarValorDominio("A", "A", 0, ativo: true).IsSuccess.Should().BeTrue();
        RegraDerivacao regra = RegraDerivacao.Criar(
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("CERTIFICADO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            "A").Value!;
        perfil.DefinirRegrasPadrao([regra], new CatalogoDeFatos([_cenario.Certificado, opcao, perfil], [])).IsSuccess.Should().BeTrue();
        perfil.Desativar().IsSuccess.Should().BeTrue();
        return perfil;
    }

}
