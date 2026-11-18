namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;


using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;

/// <summary>
/// Os grupos repetíveis, as citações entre campos e o formulário de inscrição no handler de coleta de fatos.
/// </summary>
public sealed class GruposEInscricaoNaColetaDeFatosTests
{
    [Fact(DisplayName = "Grupo de opções condicionado a campo posterior é recusado")]
    public async Task Handle_CondicaoDasOpcoesSobreCampoPosterior_Recusa()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null,
                Restricoes: [new RestricaoValorInput("OPCOES_PERMITIDAS",
                    Entradas: [new OpcoesCondicionadasInput([[Condicao("BAIXA_RENDA", "IGUAL", true)]], ["PRETA"])])]),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "NUNCA", null),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    [Theory(DisplayName = "Item cita a faixa etária quando a data de nascimento é coletada antes; depois, ou sem ela, é recusado")]
    [InlineData(0, 1, null)]
    [InlineData(2, 1, GrafoFormularioErrorCodes.CitaFatoPosterior)]
    [InlineData(-1, 1, GrafoFormularioErrorCodes.CitaFatoNaoConhecido)]
    public async Task Handle_CitaFaixaEtaria_PelaDataDeNascimento(int ordemDaData, int ordemDoItem, string? recusa)
    {
        FatoColetadoInput[] dataDeNascimento = ordemDaData < 0
            ? []
            : [new FatoColetadoInput("DATA_NASCIMENTO", ordemDaData, "Data de nascimento", "DATA", "SEMPRE", null)];
        FatoColetadoInput maiorDeIdade = new("BAIXA_RENDA", ordemDoItem, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("FAIXA_ETARIA", "MAIOR_IGUAL", 18)]]);

        Result resultado = await DefinirNaHabilitacaoAsync(ProcessoEmRascunho(), [.. dataDeNascimento, maiorDeIdade]);

        resultado.IsSuccess.Should().Be(recusa is null, resultado.Error?.Message);
        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(recusa is null ? [] : [recusa]);
    }

    [Fact(DisplayName = "Regra do item que cita fato calculado de atributos do candidato sem dependências declaradas é recusada pelo nome")]
    public async Task Handle_CitaAtributoDoCandidato_RecusaNomeada()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("FAIXA_DE_RENDA", "IGUAL", "DE_18_A_59")]]),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0]",
            Error = new { Code = GrafoFormularioErrorCodes.CitaAtributoDoCandidato },
        });
    }

    [Fact(DisplayName = "Condição sem fato é recusada, nunca estoura")]
    public async Task Handle_CondicaoSemFato_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Habilitacao,
            [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao(null!, "IGUAL", true)]])]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Acima do teto de itens, a lista é recusada inteira sem ler o catálogo")]
    public async Task Handle_AcimaDoTeto_RecusaSemLerOCatalogo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        FatoColetadoInput[] itens = [.. Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1)
            .Select(static i => new FatoColetadoInput($"FATO_{i}", i, "Campo", "BOOLEANO", "SEMPRE", null))];

        Result resultado = await HandleAsync(mocks, new DefinirFatosColetadosCommand(processo.Id, FinalidadeFormulario.Habilitacao, itens));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
        await mocks.FatoCandidatoReader.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Grupo repetível com campos de membro é aceito, com a exibição citando item anterior")]
    public async Task Handle_ComGrupo_DefineOGrupoComOsCampos()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, Composicao()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.GruposColetados.Should().ContainSingle().Which.Subitens.Select(static s => s.FatoCodigo)
            .Should().BeEquivalentTo(["PARENTESCO", "MENOR_SOB_GUARDA"]);
    }

    [Fact(DisplayName = "Campo de grupo com fato do candidato é recusado no caminho do campo")]
    public async Task Handle_CampoDeGrupoComFatoDoCandidato_RecusaNoCaminhoDoCampo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        GrupoColetadoInput grupo = Composicao() with
        {
            Subitens = [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "SEMPRE", null)],
        };

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, grupo));

        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("grupos[0].subitens[0].fatoCodigo");
        erro.Error.Code.Should().Be(ItemFormularioErrorCodes.FatoNaoColetavel);
    }

    [Fact(DisplayName = "Exibição do grupo com valor fora do domínio do fato citado é recusada no grupo")]
    public async Task Handle_ExibicaoDoGrupoInvalida_RecusaNoGrupo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        GrupoColetadoInput grupo = Composicao() with { Exibicao = [[Condicao("COR_RACA", "IGUAL", "VERDE")]] };

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, grupo));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("grupos[0].exibicao");
    }

    [Fact(DisplayName = "Exibição do grupo que cita fato calculado de atributo do candidato sem dependências declaradas é recusada, como nas demais regras")]
    public async Task Handle_ExibicaoDoGrupoCitandoAtributo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        GrupoColetadoInput grupo = Composicao() with { Exibicao = [[Condicao("FAIXA_DE_RENDA", "IGUAL", "MENOR_DE_18")]] };

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, grupo));

        resultado.Errors.Should().Contain(static e => e.Field == "grupos[0]" && e.Error.Code == GrafoFormularioErrorCodes.CitaAtributoDoCandidato);
    }

    private static GrupoColetadoInput GrupoComVariasRecusas() => Composicao() with
    {
        Minimo = 5,
        Maximo = 2,
        Exibicao = [[Condicao("COR_RACA", "IGUAL", "VERDE")]],
        Subitens =
        [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "SEMPRE", null),
            new FatoColetadoInput("MENOR_SOB_GUARDA", 1, "Menor sob guarda", "BOOLEANO", "SEMPRE", null, EtapaCodigo: FormularioDeTeste.Secao),
        ],
    };

    [Fact(DisplayName = "As recusas do grupo — forma, semântica da exibição — saem junto com as dos campos")]
    public async Task Handle_GrupoComVariasRecusas_AcumulaTodas()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        Result resultado = await HandleAsync(mocks, ComGrupo(processo, GrupoComVariasRecusas()));

        resultado.Errors.Select(static e => e.Field).Should().Contain(
            ["grupos[0].maximo", "grupos[0].exibicao", "grupos[0].subitens[0].fatoCodigo", "grupos[0].subitens[1].etapaCodigo"]);
    }

    [Fact(DisplayName = "PUT sem grupos mantém os grupos do formulário")]
    public async Task Handle_SemGrupos_MantemOsDoFormulario()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        (await HandleAsync(mocks, ComGrupo(processo, Composicao()))).IsSuccess.Should().BeTrue();

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, Composicao()) with { Grupos = null });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.GruposColetados.Should().ContainSingle();
    }

    [Fact(DisplayName = "O teto conta o grupo e os campos dele antes de ler o catálogo")]
    public async Task Handle_GrupoECamposContamNoTeto()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        FatoColetadoInput[] itens = [.. Enumerable.Range(0, FormaDoItem.MaximoDeItens - 2)
            .Select(static i => new FatoColetadoInput($"ITEM_{i}", i, "Item", "BOOLEANO", "NUNCA", null))];

        Result resultado = await HandleAsync(
            mocks, new DefinirFatosColetadosCommand(processo.Id, FinalidadeFormulario.Habilitacao, itens, [Composicao()]));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
        await mocks.FatoCandidatoReader.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campo de grupo com fato desativado é vínculo novo recusado")]
    public async Task Handle_CampoDeGrupoDesativado_RecusaVinculoNovo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
            [.. VocabularioSeed().Select(static f => f.Codigo == "MENOR_SOB_GUARDA" ? f with { Ativo = false } : f)]));

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, Composicao()));

        resultado.Error!.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "Na inscrição, o envio sem os dados básicos grava a seção e sobe os itens do cliente acima dela")]
    public async Task Handle_InscricaoSemOsDadosBasicos_GravaASecao()
    {
        ProcessoSeletivo processo = ProcessoComInscricao();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Inscricao,
        [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "SEMPRE", [[Condicao("NACIONALIDADE", "DIFERENTE", "ESTRANGEIRO")]],
                EtapaCodigo: FormularioDeTeste.Secao),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.Ordem.Should().Be(FormularioDeTeste.PrimeiraOrdemDeInscricao);
        processo.FatosColetados.Select(static f => f.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
    }

    [Fact(DisplayName = "Na inscrição, o envio que repete o formulário lido é aceito como está")]
    public async Task Handle_InscricaoRepetida_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComInscricao();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(
            processo.Id, FinalidadeFormulario.Inscricao, [.. ConjuntoBasicoDaInscricao.Itens]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Should().HaveCount(ConjuntoBasicoDaInscricao.Itens.Count);
    }

    private static FatoColetadoInput[] ItensComDadoBasicoAlteradoENaSecaoReservada() =>
    [
        ConjuntoBasicoDaInscricao.Itens.Single(static i => i.FatoCodigo == "COR_RACA") with { Obrigatoriedade = "NUNCA" },
        new FatoColetadoInput("BAIXA_RENDA", 30, "Baixa renda", "BOOLEANO", "SEMPRE", null, EtapaCodigo: ConjuntoBasicoDaInscricao.CodigoDaSecao),
    ];

    [Fact(DisplayName = "Na inscrição, o dado básico alterado e o item do cliente na seção reservada são recusados juntos")]
    public async Task Handle_InscricaoComDadoBasicoAlterado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComInscricao();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result resultado = await DefinirNaInscricaoAsync(mocks, processo, ItensComDadoBasicoAlteradoENaSecaoReservada());

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0]", EstruturaFormularioErrorCodes.DadoBasicoAlterado),
            ("itens[1].etapaCodigo", EstruturaFormularioErrorCodes.SecaoReservada),
        ]);
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "O município de nascimento cita como UF só um campo de UF do Geo")]
    [InlineData("NATURALIDADE_UF", true)]
    [InlineData("ESTADO_CIVIL", false)]
    public async Task Handle_MunicipioCitaComoUf_SoCampoDeUfDoGeo(string fatoDaUf, bool aceito)
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput(fatoDaUf, 0, "UF", "SELECAO_UNICA", "SEMPRE", null),
            new FatoColetadoInput("NATURALIDADE_MUNICIPIO", 1, "Município de nascimento", "MUNICIPIO", "SEMPRE", null,
                Restricoes: [new RestricaoValorInput("MUNICIPIOS_DA_UF", Fatos: [fatoDaUf])]),
        ]);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(aceito ? [] : [ItemFormularioErrorCodes.UfDoMunicipioInvalida]);
    }

    [Fact(DisplayName = "Na inscrição, regravar os itens sem os grupos mantém os grupos na ordem gravada")]
    public async Task Handle_InscricaoComGruposOmitidos_MantemOsGrupos()
    {
        ProcessoSeletivo processo = ProcessoComInscricao();
        Mocks mocks = NovosMocks(processo, processo.Id);
        FatoColetadoInput[] itens =
        [
            new("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "SEMPRE", null, EtapaCodigo: FormularioDeTeste.Secao),
            new("OPCAO_CURSO_1", 2, "1ª opção", "SELECAO_UNICA", "SEMPRE", null, EtapaCodigo: FormularioDeTeste.Secao),
        ];
        (await DefinirNaInscricaoAsync(mocks, processo, itens, [Composicao()])).IsSuccess.Should().BeTrue();
        int ordemGravada = processo.GruposColetados.Single().Ordem;

        Result resultado = await DefinirNaInscricaoAsync(mocks, processo, itens);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.GruposColetados.Should().ContainSingle().Which.Ordem.Should().Be(ordemGravada, "o grupo omitido fica entre os itens, onde estava");
    }
}
