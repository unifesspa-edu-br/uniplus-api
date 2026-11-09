namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class TrocaDeVigenciaTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Analisar = "selecao:processos:analisar";
    private const string Exportar = "selecao:processos:exportar";

    private static readonly FakeCatalogo Catalogo = new();

    [Fact(DisplayName = "A versão que só remove permissão não tem acréscimo")]
    public void TemAcrescimo_SoRemocao_Falso()
    {
        TrocaDeVigencia troca = EntreVersoes([Consultar, Analisar], [Consultar]);

        troca.TemAcrescimo.Should().BeFalse();
        troca.Removidas.Should().Equal(Analisar);
    }

    [Fact(DisplayName = "A versão que só muda o nome não tem acréscimo")]
    public void TemAcrescimo_SoNome_Falso() =>
        TrocaPara([Consultar, Analisar], [Consultar, Analisar], "Acompanhamento ampliado").TemAcrescimo.Should().BeFalse();

    [Fact(DisplayName = "A versão que remove e acrescenta tem acréscimo")]
    public void TemAcrescimo_RemoveEAcrescenta_Verdadeiro()
    {
        TrocaDeVigencia troca = EntreVersoes([Consultar, Analisar], [Consultar, Exportar]);

        troca.TemAcrescimo.Should().BeTrue();
        troca.Removidas.Should().Equal(Analisar);
    }

    [Fact(DisplayName = "A versão que só acrescenta tem acréscimo")]
    public void TemAcrescimo_SoAcrescimo_Verdadeiro() =>
        TrocaPara([Consultar], [Consultar, Analisar]).TemAcrescimo.Should().BeTrue();

    [Fact(DisplayName = "A primeira vigência não tem versão anterior nem acréscimo")]
    public void PrimeiraVigencia_SemAnterior_SemAcrescimo()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Acompanhamento", "Acompanha os atendimentos.", [Consultar], Catalogo);

        TrocaDeVigencia troca = PerfisDeTeste.EntrarEmVigor(perfil, Catalogo).Troca!;

        troca.Anterior.Should().BeNull();
        troca.Nova.Numero.Should().Be(1);
        troca.TemAcrescimo.Should().BeFalse();
    }

    [Fact(DisplayName = "A troca aponta a versão que saiu e a que entrou, e o veredicto repetido não a devolve")]
    public void RegistrarVerificado_DevolveAVersaoQueSaiuEAQueEntrou()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os atendimentos.", [Consultar], Catalogo);
        VersaoDoPerfil primeira = perfil.VersaoVigente!;

        TrocaDeVigencia troca = TrocaPara(perfil, [Consultar, Analisar]);

        troca.Anterior.Should().BeSameAs(primeira);
        troca.Nova.Should().BeSameAs(perfil.VersaoVigente);
        troca.Nova.Numero.Should().Be(2);
        primeira.Situacao.Should().Be(SituacaoDaVersao.Encerrada);
    }

    // A proposta não retira permissão, mas a troca compara quaisquer duas versões: a de remoção também passa por aqui.
    private static TrocaDeVigencia EntreVersoes(string[] antes, string[] depois) =>
        new(
            PerfisDeTeste.Vigente("Antes", "Acompanha os atendimentos.", antes, Catalogo).VersaoVigente,
            PerfisDeTeste.Vigente("Depois", "Acompanha os atendimentos.", depois, Catalogo).VersaoVigente!);

    private static TrocaDeVigencia TrocaPara(string[] antes, string[] depois, string? nome = null)
    {
        Perfil perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os atendimentos.", antes, Catalogo);

        return TrocaPara(perfil, depois, nome);
    }

    private static TrocaDeVigencia TrocaPara(Perfil perfil, string[] permissoes, string? nome = null)
    {
        perfil.CriarVersao(
            PerfisDeTeste.Conteudo(nome ?? perfil.Nome, perfil.Descricao, permissoes, Catalogo),
            PerfisDeTeste.SemContexto,
            PerfisDeTeste.AutoriaDoAutor,
            PerfisDeTeste.Instante,
            Catalogo).IsSuccess.Should().BeTrue();

        return PerfisDeTeste.EntrarEmVigor(perfil, Catalogo).Troca!;
    }

    private sealed class FakeCatalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("selecao:processos:", StringComparison.Ordinal);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => ClassificacaoDeProtecao.Interno;
    }
}
