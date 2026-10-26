namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Leiautes;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// O cruzamento das linhas do retorno com as inscrições da edição: quem recebe nota, quem fica sem
/// linha, a linha de quem não é inscrito e o conflito entre linhas diferentes do mesmo CPF.
/// </summary>
public sealed class CruzamentoDasNotasComInscritosTests
{
    private static readonly IReadOnlyList<PersonasDeTeste.PessoaFicticia> Pessoas = PersonasDeTeste.Candidatas;
    private static readonly Dictionary<string, Guid> Inscricoes = Pessoas.ToDictionary(p => p.Cpf, _ => Guid.CreateVersion7());

    private static string MascaraEsperada(PersonasDeTeste.PessoaFicticia pessoa) =>
        $"***.{pessoa.Cpf[3..6]}.{pessoa.Cpf[6..9]}-**";

    private static IndiceCegoDoCpf Indice(string cpf) => IndiceCegoDoCpf.De(Encoding.UTF8.GetBytes($"indice:{cpf}"));

    private static ResultadoDoCruzamento Cruzar(IEnumerable<string> linhasDoArquivo, params int[] inscritos)
    {
        IReadOnlyList<LinhaDeRetorno> linhas = LeitorDeArquivoDoInep
            .Ler(Bytes(linhasDoArquivo), CatalogoDeLeiautesDoInep.EnemRetornoV1).Value!.Linhas;
        Dictionary<string, IndiceCegoDoCpf> indices = linhas.Select(l => l.Cpf).Distinct().ToDictionary(c => c, Indice);
        InscritoDaEdicao[] inscritosDaEdicao = [.. inscritos.Select(i => new InscritoDaEdicao(Inscricoes[Pessoas[i].Cpf], Indice(Pessoas[i].Cpf), []))];

        return CruzamentoDasNotasComInscritos.Cruzar(linhas, indices, inscritosDaEdicao);
    }

    [Fact(DisplayName = "inscrito com uma linha recebe nota nas cinco áreas e guarda o número do INEP")]
    public void Cruzar_CaminhoFeliz_CincoAreas()
    {
        ResultadoDoCruzamento resultado = Cruzar([Linha(Pessoas[0], 1)], 0);

        InscritoComNota inscrito = resultado.ComNota.Should().ContainSingle().Subject;
        inscrito.InscricaoId.Should().Be(Inscricoes[Pessoas[0].Cpf]);
        inscrito.NumeroDeInscricaoNoEnem.Should().Be(NumeroDeInscricao(1));
        inscrito.Areas.Should().HaveCount(5);
        resultado.InscritosSemLinha.Should().BeEmpty();
        resultado.LinhasSemInscrito.Should().BeEmpty();
        resultado.Conflitos.Should().BeEmpty();
    }

    [Fact(DisplayName = "inscrito da edição que não está no arquivo fica sem linha")]
    public void Cruzar_InscritoSemLinha()
    {
        ResultadoDoCruzamento resultado = Cruzar([Linha(Pessoas[0], 1)], 0, 1);

        resultado.InscritosSemLinha.Should().Equal(Inscricoes[Pessoas[1].Cpf]);
    }

    [Fact(DisplayName = "CPF sem inscrito vira ocorrência com o número da linha, o CPF mascarado, o nome e o sexo do arquivo")]
    public void Cruzar_LinhaSemInscrito_IdentificaAPessoaPeloArquivo()
    {
        ResultadoDoCruzamento resultado = Cruzar([Linha(Pessoas[0], 1), Linha(Pessoas[2], 2)], 0);

        LinhaSemInscritoNoCruzamento linha = resultado.LinhasSemInscrito.Should().ContainSingle().Subject;
        linha.NumeroDaLinha.Should().Be(2);
        linha.Nome.Should().Be(Pessoas[2].Nome);
        linha.Sexo.Should().Be("F");
        linha.CpfMascarado.Should().Be(MascaraEsperada(Pessoas[2]));
        resultado.ComNota.Should().ContainSingle();
    }

    [Fact(DisplayName = "duas linhas idênticas do mesmo CPF contam como uma e geram lançamento")]
    public void Cruzar_LinhasIdenticas_ContamComoUma()
    {
        ResultadoDoCruzamento resultado = Cruzar([Linha(Pessoas[0], 1), Linha(Pessoas[0], 1)], 0);

        resultado.ComNota.Should().ContainSingle();
        resultado.Conflitos.Should().BeEmpty();
    }

    [Fact(DisplayName = "duas linhas diferentes do mesmo CPF inscrito são conflito, sem lançamento e sem 'sem linha'")]
    public void Cruzar_LinhasDiferentes_Conflito()
    {
        string[] outraNota = ["100", "722.2", "833.3", "544.4", "960"];

        ResultadoDoCruzamento resultado = Cruzar([Linha(Pessoas[0], 1), Linha(Pessoas[1], 2), Linha(Pessoas[0], 1, outraNota)], 0, 1);

        ConflitoDeLinhas conflito = resultado.Conflitos.Should().ContainSingle().Subject;
        conflito.InscricaoId.Should().Be(Inscricoes[Pessoas[0].Cpf]);
        conflito.NumerosDasLinhas.Should().Equal(1, 3);
        resultado.ComNota.Select(c => c.InscricaoId).Should().Equal(Inscricoes[Pessoas[1].Cpf]);
        resultado.InscritosSemLinha.Should().BeEmpty();
    }

    [Fact(DisplayName = "CPF repetido com linhas diferentes que não é inscrito vira uma ocorrência por linha, não conflito")]
    public void Cruzar_RepetidoNaoInscrito_UmaOcorrenciaPorLinha()
    {
        string[] outraNota = ["100", "722.2", "833.3", "544.4", "960"];

        ResultadoDoCruzamento resultado = Cruzar([Linha(Pessoas[2], 1), Linha(Pessoas[2], 2, outraNota)], 0);

        resultado.LinhasSemInscrito.Select(l => l.NumeroDaLinha).Should().Equal(1, 2);
        resultado.LinhasSemInscrito.Should().OnlyContain(l => l.CpfMascarado == MascaraEsperada(Pessoas[2]));
        resultado.Conflitos.Should().BeEmpty();
        resultado.InscritosSemLinha.Should().Equal(Inscricoes[Pessoas[0].Cpf]);
    }

    [Fact(DisplayName = "linhas do mesmo CPF que só diferem em colunas ignoradas não geram conflito")]
    public void Cruzar_ColunasIgnoradasDiferentes_NaoGeramConflito()
    {
        ResultadoDoCruzamento resultado = Cruzar([Linha(Pessoas[0], 1), Linha(Pessoas[0], 1, nome: "Nome Digitado Diferente")], 0);

        resultado.ComNota.Should().ContainSingle();
        resultado.Conflitos.Should().BeEmpty();
    }

    [Fact(DisplayName = "linhas do mesmo CPF com a mesma nota escrita com e sem casa decimal não geram conflito")]
    public void Cruzar_MesmaNotaEmRepresentacoesDiferentes_NaoGeraConflito()
    {
        string[] mesmaNotaComCasa = ["611.1", "722.2", "833.3", "544.4", "0960.0"];

        ResultadoDoCruzamento resultado = Cruzar([Linha(Pessoas[0], 1), Linha(Pessoas[0], 1, mesmaNotaComCasa)], 0);

        resultado.ComNota.Should().ContainSingle();
        resultado.Conflitos.Should().BeEmpty();
    }
}
