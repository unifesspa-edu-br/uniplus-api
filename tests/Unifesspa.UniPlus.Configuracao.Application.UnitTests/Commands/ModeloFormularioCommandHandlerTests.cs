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

/// <summary>
/// A escrita do modelo de formulário confere o conteúdo contra o catálogo de fatos, de termos e de
/// tipos de processo pelas mesmas regras do formulário do processo (UNI-REQ-0144).
/// </summary>
public sealed class ModeloFormularioCommandHandlerTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact(DisplayName = "Cria o modelo conferido contra o catálogo, com o formato do campo de texto vindo do fato")]
    public async Task Criar_ConteudoValido_GravaComOFormatoDoCatalogo()
    {
        ModeloFormulario? gravado = null;
        await _cenario.Repository.AdicionarAsync(Arg.Do<ModeloFormulario>(m => gravado = m), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CPF_RESPONSAVEL", 0, "TEXTO")), tipo: "REGULAR_UNIFICADO");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        gravado!.Conteudo.Itens.Single().Formato.Should().Be("CPF");
    }

    [Fact(DisplayName = "O descritivo ausente na criação recusa o nome obrigatório no caminho do descritivo")]
    public async Task Criar_DescritivoAusente_RecusaONomeNoCaminhoDoDescritivo()
    {
        CriarModeloFormularioCommand comando = new("HABILITACAO_MEDICINA", "HABILITACAO", null, Conteudo(Item("CERTIFICADO", 0)));

        Result<Guid> resultado = await CriarModeloFormularioCommandHandler.Handle(
            comando, _cenario.Repository, _cenario.Fatos, _cenario.Termos, _cenario.Tipos, _cenario.UnitOfWork, CancellationToken.None);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "descritivo.nome" && e.Error.Code == ModeloFormularioErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "O tipo de processo inexistente é recusado no caminho do descritivo")]
    public async Task Criar_TipoDeProcessoInexistente_RecusaNoCaminhoDoDescritivo()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)), tipo: "NAO_EXISTE");

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "descritivo.tipoProcessoCodigo" && e.Error.Code == ModeloFormularioErrorCodes.TipoProcessoInexistente);
    }

    [Fact(DisplayName = "O campo do grupo cita o campo anterior da mesma ocorrência, e o grupo é gravado")]
    public async Task Criar_CampoDoGrupoCitaCampoAnterior_Grava()
    {
        ModeloFormulario? gravado = null;
        await _cenario.Repository.AdicionarAsync(Arg.Do<ModeloFormulario>(m => gravado = m), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await _cenario.CriarAsync(ComComposicao(
            Campo("MAIOR_IDADE", 0), Montar("SEM_RENDA", 1).NaSecao(null).ExibidoQuando(Quando("MAIOR_IDADE", true)).Construir()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        gravado!.Conteudo.Grupos.Single().Subitens.Select(static s => s.FatoCodigo).Should().Equal("MAIOR_IDADE", "SEM_RENDA");
    }

    [Fact(DisplayName = "Grupo que inclui o candidato sem o campo de parentesco é recusado no modelo")]
    public async Task Criar_GrupoQueIncluiOCandidatoSemParentesco_Recusa()
    {
        ConteudoDoModeloInput conteudo = ComComposicao(Campo("MAIOR_IDADE", 0));
        conteudo = conteudo with { Grupos = [conteudo.Grupos![0] with { Ocorrencias = new OcorrenciasDoGrupoInput(1, IncluiCandidato: true, Maximo: 10) }] };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "conteudo.grupos[0].subitens" && e.Error.Code == GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto);
    }

    [Fact(DisplayName = "Grupo que inclui o candidato sem o mínimo é recusado só no mínimo, e o modelo não é gravado (acúmulo das recusas de leitura e de domínio)")]
    public async Task Criar_GrupoSemMinimoComInclusaoDoCandidato_RecusaSoOMinimo()
    {
        ConteudoDoModeloInput conteudo = ComComposicao(Campo("MAIOR_IDADE", 0));
        conteudo = conteudo with { Grupos = [conteudo.Grupos![0] with { Ocorrencias = new OcorrenciasDoGrupoInput(null, IncluiCandidato: true, Maximo: 10) }] };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        FieldError recusa = resultado.Errors.Should().ContainSingle(static e => e.Field == "conteudo.grupos[0].ocorrencias.minimo").Subject;
        recusa.Error.Code.Should().Be(GrupoFormularioErrorCodes.OcorrenciasObrigatorias);
        await _cenario.Repository.DidNotReceive().AdicionarAsync(Arg.Any<ModeloFormulario>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Grupo sem as ocorrências é recusado no mínimo e na inclusão do candidato, antes das regras do grupo")]
    public async Task Criar_GrupoSemOcorrencias_RecusaOMinimoEAInclusaoAntesDasRegras()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(SemOcorrenciasEComObrigatoriedadeInvalida(ComComposicao(Campo("MAIOR_IDADE", 0))));

        resultado.Errors.Select(static e => e.Field).Should().ContainInOrder(
            "conteudo.grupos[0].ocorrencias.minimo", "conteudo.grupos[0].ocorrencias.incluiCandidato", "conteudo.grupos[0].regras.obrigatoriedade");
        resultado.Errors.Take(2).Select(static e => e.Error.Code).Should().AllBe(GrupoFormularioErrorCodes.OcorrenciasObrigatorias);
        await _cenario.Repository.DidNotReceive().AdicionarAsync(Arg.Any<ModeloFormulario>(), Arg.Any<CancellationToken>());
    }

    private static ConteudoDoModeloInput SemOcorrenciasEComObrigatoriedadeInvalida(ConteudoDoModeloInput conteudo)
    {
        GrupoColetadoInput grupo = conteudo.Grupos![0];
        return conteudo with { Grupos = [grupo with { Ocorrencias = null, Regras = grupo.Regras! with { Obrigatoriedade = new ObrigatoriedadeInput("INEXISTENTE") } }] };
    }

    [Fact(DisplayName = "Campo do grupo com fato do candidato não é coletável na ocorrência")]
    public async Task Criar_CampoDoGrupoComFatoDoCandidato_Recusa()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(ComComposicao(Campo("CPF_RESPONSAVEL", 0, "TEXTO")));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("conteudo.grupos[0].subitens[0].fatoCodigo");
    }

    [Fact(DisplayName = "Campo do grupo com fato de membro desativado é recusado como vínculo novo")]
    public async Task Criar_CampoDoGrupoDesativado_Recusa()
    {
        _cenario.MaiorIdade.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> resultado = await _cenario.CriarAsync(ComComposicao(Campo("MAIOR_IDADE", 0)));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "A condição de um documento do conjunto padrão é conferida contra o catálogo na gravação do modelo")]
    public async Task Criar_DocumentoCitaFatoForaDoCatalogo_RecusaNoCampoDoDocumento()
    {
        ConteudoDoModeloInput conteudo = Conteudo(Item("CERTIFICADO", 0)) with
        {
            ConjuntoDeDocumentos = ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(new DocumentoExigidoDoModeloInput("INSCRICAO", "LAUDO_MEDICO", Quando("NAO_EXISTE", true), new IndeferimentoDoDocumentoDoModeloInput(false, "RETIRA_O_ACRESCIMO", []))),
        };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        resultado.Errors.Should().Contain(static e => e.Field == "conteudo.conjuntoDeDocumentos.documentosExigidos[0].quando" && e.Error.Code == "PredicadoDnf.FatoDesconhecido");
    }

    [Theory(DisplayName = "O documento só se multiplica pelas opções de fato cujas opções o processo declara")]
    [InlineData("CONDICAO", null)]
    [InlineData(" CONDICAO ", ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido)]
    [InlineData("CERTIFICADO", ModeloFormularioErrorCodes.ExigenciaPorOpcaoInvalida)]
    [InlineData("NAO_EXISTE", ItemFormularioErrorCodes.FatoDesconhecido)]
    public async Task Criar_DocumentoPorOpcao_SoComOpcoesDoProcesso(string fato, string? recusa)
    {
        Result<Guid> resultado = await _cenario.CriarAsync(ComDocumentoPorOpcao(fato));

        if (recusa is null)
        {
            resultado.IsSuccess.Should().BeTrue(string.Join("; ", resultado.Errors.Select(static e => $"{e.Field}: {e.Error.Code}")));
        }
        else
        {
            resultado.Errors.Should().Contain(e => e.Field == "conteudo.conjuntoDeDocumentos.documentosExigidos[0].porOpcao.fatoCodigo" && e.Error.Code == recusa);
        }
    }

    [Theory(DisplayName = "Regra cita a faixa etária quando a data de nascimento é coletada antes; sem ela, é recusada")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Criar_RegraCitaFaixaEtaria_PelaDataDeNascimento(bool coletaDataDeNascimento)
    {
        FatoColetadoInput cita = Montar("CERTIFICADO", 1).ExibidoQuando(Quando("FAIXA_ETARIA", 18)).Construir();
        ConteudoDoModeloInput conteudo = coletaDataDeNascimento ? Conteudo(Item("DATA_NASCIMENTO", 0, "DATA"), cita) : Conteudo(cita);

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        if (coletaDataDeNascimento)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        }
        else
        {
            resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
        }
    }

    [Fact(DisplayName = "Item de fato calculado pelo sistema não é coletável")]
    public async Task Criar_ItemDeFatoCalculado_Recusa()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("IDADE", 0, "NUMERO")));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.itens[0].fatoCodigo", new DomainError(ItemFormularioErrorCodes.FatoNaoColetavel, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message).Excluding(static e => e.Origem));
    }

    [Fact(DisplayName = "Regra que cita valor fora do tipo do fato é recusada pela semântica do predicado")]
    public async Task Criar_RegraComValorDeOutroTipo_Recusa()
    {
        FatoColetadoInput exibido = Montar("CPF_RESPONSAVEL", 1, "TEXTO").ExibidoQuando(Quando("CERTIFICADO", "SIM")).Construir();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0), exibido));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("conteudo.itens[1].regras.exibicao");
    }

    [Fact(DisplayName = "Regra que cita fato calculado de atributos do candidato é recusada")]
    public async Task Criar_RegraCitaAtributo_Recusa()
    {
        FatoColetadoInput exibido = Montar("CERTIFICADO", 0).ExibidoQuando(Quando("IDADE", 18)).Construir();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(exibido));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should()
            .Contain(("conteudo.itens[0].regras.exibicao", GrafoFormularioErrorCodes.CitaAtributoDoCandidato));
    }

    [Fact(DisplayName = "Termo cuja versão não existe no catálogo é recusado")]
    public async Task Criar_VersaoDeTermoInexistente_Recusa()
    {
        TermoExigidoInput termo = EntradaDoTermoExigidoDeTeste.Exigindo("LGPD").DaVersao(Guid.NewGuid(), Guid.NewGuid()).Construir();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Termos = [termo] });

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.termos[0].termo.versaoId", new DomainError(ModeloFormularioErrorCodes.TermoVersaoNaoEncontrada, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message).Excluding(static e => e.Origem));
    }

    private ConteudoDoModeloInput ComDocumentoPorOpcao(string fato)
    {
        FatoCandidato condicao = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato("CONDICAO", "Condição", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.Opcao, QuantidadeDeValores.VariosValores, FonteValoresFato.OpcoesDoProcesso, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;
        Aprovar(condicao);
        _cenario.Fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [_cenario.Certificado, condicao]);
        return Conteudo(Item("CERTIFICADO", 0), Item("CONDICAO", 1, "ESCOLHA_MULTIPLA")) with
        {
            ConjuntoDeDocumentos = ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(new DocumentoExigidoDoModeloInput("INSCRICAO", "LAUDO_MEDICO", Quando("CERTIFICADO", true), new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", []), PorOpcao: new ExigenciaPorOpcaoInput(fato, []))),
        };

    }
}
