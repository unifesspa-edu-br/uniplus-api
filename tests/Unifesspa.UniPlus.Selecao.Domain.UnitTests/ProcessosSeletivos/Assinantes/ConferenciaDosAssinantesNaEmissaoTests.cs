namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Assinantes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Assinantes.UnidadesDosAssinantesParaTestes;

/// <summary>A conferência de quem assina no instante de emitir o documento da fase.</summary>
public sealed class ConferenciaDosAssinantesNaEmissaoTests
{
    private static Result<AssinantesDaEmissao> Conferir(
        string[] codigos, EstruturaDasUnidadesAtivas estrutura, params PessoaDoCadastroDeAssinantes[] cadastro) =>
        ConferenciaDosAssinantesNaEmissao.Conferir(AssinantesDaFase.Criar(codigos), CoordenadoriaDeEnsino, Cadastro(cadastro), estrutura);

    private static readonly PessoaDoCadastroDeAssinantes Betina = Pessoa("betina", "Betina Fictícia", Cga);
    private static readonly PessoaDoCadastroDeAssinantes Breno = Pessoa("breno", "Breno Fictício", CoordenadoriaDeEnsino);

    [Fact(DisplayName = "Emissão sem assinantes é recusada")]
    public void EmissaoSemAssinantes_ERecusada() =>
        Conferir([], Estrutura()).Error!.Code.Should().Be(AssinantesErrorCodes.EmissaoSemAssinantes);

    [Fact(DisplayName = "Quem foi transferido para fora da hierarquia é recusado pela posição 2, sem o nome")]
    public void PessoaTransferida_ERecusadaPelaPosicaoSemONome()
    {
        PessoaDoCadastroDeAssinantes transferido = Pessoa("breno", "Breno Fictício", Ceps);

        Result<AssinantesDaEmissao> resultado = Conferir(["betina", "breno"], Estrutura(), Betina, transferido);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "/assinantes/1" && e.Error.Code == AssinantesErrorCodes.SemAutoridadePelaUnidadeDaFase);
        resultado.Error!.Message.Should().Contain("posição 2").And.NotContain("Breno");
    }

    [Fact(DisplayName = "Quem foi desativado depois da configuração é recusado na emissão")]
    public void PessoaDesativada_ERecusada() =>
        Conferir(["betina", "breno"], Estrutura(), Betina, Pessoa("breno", "Breno Fictício", CoordenadoriaDeEnsino, ativa: false))
            .Error!.Code.Should().Be(AssinantesErrorCodes.Inativo);

    [Fact(DisplayName = "Quem perdeu o aceite depois da configuração é recusado na emissão")]
    public void PessoaQuePerdeuOAceite_ERecusada() =>
        Conferir(["betina"], Estrutura(), Pessoa("betina", "Betina Fictícia", Cga, aceiteVigente: false))
            .Error!.Code.Should().Be(AssinantesErrorCodes.SemAceiteVigente);

    [Fact(DisplayName = "Unidade intermediária inativada depois da configuração faz a emissão ser recusada")]
    public void UnidadeIntermediariaInativada_FazAEmissaoSerRecusada()
    {
        PessoaDoCadastroDeAssinantes naSecao = Pessoa("breno", "Breno Fictício", SecaoDeEditais);

        Result<AssinantesDaEmissao> resultado = ConferenciaDosAssinantesNaEmissao.Conferir(
            AssinantesDaFase.Criar(["breno"]), SubsecaoDeProvas, Cadastro(naSecao), EstruturaSem(SecaoDeEditais));

        resultado.Error!.Code.Should().Be(AssinantesErrorCodes.SemAutoridadePelaUnidadeDaFase);
    }

    [Fact(DisplayName = "Emissão aceita devolve os assinantes na ordem da lista, com o nome do cadastro de agora")]
    public void EmissaoAceita_DevolveOsAssinantesNaOrdemDaLista()
    {
        Result<AssinantesDaEmissao> resultado = Conferir(["breno", "betina"], Estrutura(), Betina, Breno);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Assinantes.Should().Equal(
            new AssinanteDaEmissao(1, "breno", "Breno Fictício"),
            new AssinanteDaEmissao(2, "betina", "Betina Fictícia"));
    }

    [Fact(DisplayName = "A cópia da emissão não muda quando o cadastro muda depois")]
    public void CopiaDaEmissao_NaoMudaComOCadastro()
    {
        Dictionary<string, PessoaDoCadastroDeAssinantes> cadastro = Cadastro(Betina);
        AssinantesDaEmissao emitidos = ConferenciaDosAssinantesNaEmissao.Conferir(
            AssinantesDaFase.Criar(["betina"]), CoordenadoriaDeEnsino, cadastro, Estrutura()).Value!;

        cadastro["betina"] = Pessoa("betina", "Betina Renomeada", Ceps, ativa: false);

        emitidos.Assinantes.Should().Equal(new AssinanteDaEmissao(1, "betina", "Betina Fictícia"));
    }

    [Fact(DisplayName = "Hierarquia com ciclo na emissão falha com erro explícito")]
    public void HierarquiaComCiclo_FalhaNaEmissao()
    {
        Guid primeira = Guid.NewGuid();
        Guid segunda = Guid.NewGuid();
        Guid fora = Guid.NewGuid();
        EstruturaDasUnidadesAtivas estrutura = new([new(primeira, segunda), new(segunda, primeira), new(fora, null)]);

        Result<AssinantesDaEmissao> resultado = ConferenciaDosAssinantesNaEmissao.Conferir(
            AssinantesDaFase.Criar(["betina"]), primeira, Cadastro(Pessoa("betina", "Betina Fictícia", fora)), estrutura);

        resultado.Error!.Code.Should().Be(AssinantesErrorCodes.HierarquiaComCiclo);
    }

    [Fact(DisplayName = "A cópia da emissão não deixa recuperar o armazenamento para trocar assinantes")]
    public void CopiaDaEmissao_NaoExpoeOArmazenamentoMutavel()
    {
        AssinantesDaEmissao emitidos = Conferir(["betina"], Estrutura(), Betina).Value!;

        (emitidos.Assinantes is AssinanteDaEmissao[]).Should().BeFalse();
        (emitidos.Assinantes is IList<AssinanteDaEmissao> { IsReadOnly: false }).Should().BeFalse();
    }
}
