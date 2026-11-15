namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Assinantes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Assinantes.UnidadesDosAssinantesParaTestes;

/// <summary>A lista de assinantes de uma fase: a ordem que guarda e as recusas de cada posição.</summary>
public sealed class RegrasDosAssinantesTests
{
    private const string CaminhoDaLista = "/cronograma/fases/0/assinantes";

    private static Result Conferir(string[] codigos, Guid? unidadeDaFase, params PessoaDoCadastroDeAssinantes[] cadastro) =>
        RegrasDosAssinantes.Conferir(codigos, unidadeDaFase, Cadastro(cadastro), Estrutura(), CaminhoDaLista);

    [Fact(DisplayName = "A lista guarda a ordem declarada: Betina na primeira posição e Breno na segunda")]
    public void Lista_GuardaAOrdemDeclarada()
    {
        AssinantesDaFase lista = AssinantesDaFase.Criar(["betina", "breno"]);

        lista.Codigos.Should().Equal("betina", "breno");
    }

    [Fact(DisplayName = "Mudar a coleção de origem depois não altera a lista criada")]
    public void Lista_NaoAcompanhaAColecaoDeOrigem()
    {
        List<string> origem = ["betina", "breno"];
        AssinantesDaFase lista = AssinantesDaFase.Criar(origem);

        origem.Reverse();

        lista.Codigos.Should().Equal("betina", "breno");
    }

    [Fact(DisplayName = "Lista com pessoas ativas, com aceite e com autoridade pela unidade da fase é aceita")]
    public void ListaConforme_EAceita() =>
        Conferir(
            ["betina", "breno"],
            CoordenadoriaDeEnsino,
            Pessoa("betina", "Betina", CoordenadoriaDeEnsino),
            Pessoa("breno", "Breno", Cga)).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "A mesma pessoa duas vezes é recusada no caminho da segunda ocorrência")]
    public void PessoaRepetida_ERecusadaNoCaminhoDaSegunda()
    {
        Result resultado = Conferir(["betina", "breno", "betina"], Cga, Pessoa("betina", "Betina", Cga), Pessoa("breno", "Breno", Cga));

        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "/cronograma/fases/0/assinantes/2" && e.Error.Code == AssinantesErrorCodes.Repetido);
    }

    [Fact(DisplayName = "Pessoa inativa é recusada pela posição, com o código de inativa")]
    public void PessoaInativa_ERecusadaPelaPosicao()
    {
        Result resultado = Conferir(["betina", "breno"], Cga, Pessoa("betina", "Betina", Cga), Pessoa("breno", "Breno", Cga, ativa: false));

        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "/cronograma/fases/0/assinantes/1" && e.Error.Code == AssinantesErrorCodes.Inativo);
    }

    [Fact(DisplayName = "Pessoa com aceite pendente é recusada pela posição, com o código de sem aceite")]
    public void PessoaComAceitePendente_ERecusada()
    {
        Result resultado = Conferir(["isabella"], Cga, Pessoa("isabella", "Isabella", Cga, aceiteVigente: false));

        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "/cronograma/fases/0/assinantes/0" && e.Error.Code == AssinantesErrorCodes.SemAceiteVigente);
    }

    [Fact(DisplayName = "Pessoa inativa e sem aceite recebe só a recusa de inativa, a primeira que orienta")]
    public void PessoaInativaESemAceite_RecebeSoAInativa() =>
        Conferir(["breno"], Cga, Pessoa("breno", "Breno", Cga, ativa: false, aceiteVigente: false))
            .Error!.Code.Should().Be(AssinantesErrorCodes.Inativo);

    [Fact(DisplayName = "Pessoa inativa listada duas vezes: a primeira recebe inativa e a segunda, repetida")]
    public void PessoaInativaRepetida_RecebeInativaEDepoisRepetida()
    {
        Result resultado = Conferir(["breno", "breno"], Cga, Pessoa("breno", "Breno", Cga, ativa: false));

        resultado.Errors.Select(e => e.Error.Code).Should().Equal(AssinantesErrorCodes.Inativo, AssinantesErrorCodes.Repetido);
    }

    [Fact(DisplayName = "Pessoa fora do cadastro é recusada com o código próprio de não cadastrada")]
    public void PessoaForaDoCadastro_ERecusada() =>
        Conferir(["desconhecida"], Cga).Error!.Code.Should().Be(AssinantesErrorCodes.NaoCadastrado);

    [Fact(DisplayName = "Pessoa sem autoridade pela unidade da fase é recusada: Caroline, do CEPS, não assina pela CGA")]
    public void PessoaSemAutoridade_ERecusada() =>
        Conferir(["caroline"], Cga, Pessoa("caroline", "Caroline", Ceps))
            .Error!.Code.Should().Be(AssinantesErrorCodes.SemAutoridadePelaUnidadeDaFase);

    [Fact(DisplayName = "Pessoa de unidade acima da fase tem autoridade e pessoa de unidade abaixo não tem")]
    public void Autoridade_ConfereContraAUnidadeDaFase()
    {
        PessoaDoCadastroDeAssinantes acima = Pessoa("isabella", "Isabella", Cga);
        PessoaDoCadastroDeAssinantes abaixo = Pessoa("breno", "Breno", SecaoDeEditais);

        Conferir(["isabella"], CoordenadoriaDeEnsino, acima, abaixo).IsSuccess.Should().BeTrue();
        Conferir(["breno"], CoordenadoriaDeEnsino, acima, abaixo).Error!.Code
            .Should().Be(AssinantesErrorCodes.SemAutoridadePelaUnidadeDaFase);
    }

    [Fact(DisplayName = "Fase sem unidade não acusa falta de autoridade: a falta da unidade é a recusa dela")]
    public void FaseSemUnidade_NaoAcusaFaltaDeAutoridade() =>
        Conferir(["caroline"], unidadeDaFase: null, Pessoa("caroline", "Caroline", Ceps)).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "A mesma pessoa em fases diferentes é aceita: a repetição vale só dentro da lista")]
    public void MesmaPessoaEmFasesDiferentes_EAceita()
    {
        PessoaDoCadastroDeAssinantes betina = Pessoa("betina", "Betina", Cga);

        Conferir(["betina"], Cga, betina).IsSuccess.Should().BeTrue();
        Conferir(["betina"], CoordenadoriaDeRegistro, betina).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A mensagem da recusa cita a posição e nunca o código nem o nome da pessoa")]
    public void MensagemDaRecusa_CitaAPosicaoENuncaOCodigoNemONome()
    {
        Result resultado = Conferir(["breno-codigo"], Cga, Pessoa("breno-codigo", "Breno Fictício", Cga, ativa: false));

        string mensagem = resultado.Error!.Message;
        mensagem.Should().Contain("posição 1");
        mensagem.Should().NotContain("Breno").And.NotContain("breno-codigo");
    }

    [Fact(DisplayName = "Hierarquia com ciclo falha com erro explícito, sem recusa de posição")]
    public void HierarquiaComCiclo_FalhaComErroExplicito()
    {
        Guid primeira = Guid.NewGuid();
        Guid segunda = Guid.NewGuid();
        Guid fora = Guid.NewGuid();
        EstruturaDasUnidadesAtivas estrutura = new([new(primeira, segunda), new(segunda, primeira), new(fora, null)]);

        Result resultado = RegrasDosAssinantes.Conferir(
            ["betina"], primeira, Cadastro(Pessoa("betina", "Betina", fora)), estrutura, CaminhoDaLista);

        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == null && e.Error.Code == AssinantesErrorCodes.HierarquiaComCiclo);
    }

    [Fact(DisplayName = "A lista não deixa recuperar o armazenamento para trocar códigos ou a ordem")]
    public void Lista_NaoExpoeOArmazenamentoMutavel()
    {
        AssinantesDaFase lista = AssinantesDaFase.Criar(["betina", "breno"]);

        (lista.Codigos is string[]).Should().BeFalse();
        (lista.Codigos is IList<string> { IsReadOnly: false }).Should().BeFalse();
    }
}
