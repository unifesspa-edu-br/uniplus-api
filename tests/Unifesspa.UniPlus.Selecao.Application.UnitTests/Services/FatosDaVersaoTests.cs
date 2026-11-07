namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services;

/// <summary>Os fatos congelados respondem qual grupo abriga um identificador e qual proteção o nome tem.</summary>
public sealed class FatosDaVersaoTests
{
    [Fact(DisplayName = "O mesmo fato identificador em dois grupos repetíveis aparece nos dois, para a ocorrência escolher o grupo")]
    public void Identificadores_FatoEmDoisGrupos_DevolveOsDois()
    {
        FatosDaVersao fatos = new(
            new Dictionary<string, FatoDoProcesso>(),
            [Grupo("COMPOSICAO_FAMILIAR", Fato("RG_NUMERO", ClassificacaoDeProtecao.Identificador)), Grupo("RESPONSAVEIS", Fato("RG_NUMERO", ClassificacaoDeProtecao.Identificador))],
            new Dictionary<string, FatoDoProcesso>(),
            new Dictionary<string, string>());

        fatos.Identificadores("RG_NUMERO", emGrupo: true).Select(static c => c.GrupoCodigo).Should().Equal("COMPOSICAO_FAMILIAR", "RESPONSAVEIS");
    }

    [Fact(DisplayName = "O fato na raiz e num grupo: com ocorrência só o grupo, sem ocorrência só a raiz")]
    public void Identificadores_RaizEGrupo_EscolhePelaOcorrencia()
    {
        FatosDaVersao fatos = new(
            new Dictionary<string, FatoDoProcesso> { ["RG_NUMERO"] = Fato("RG_NUMERO", ClassificacaoDeProtecao.Identificador) },
            [Grupo("COMPOSICAO_FAMILIAR", Fato("RG_NUMERO", ClassificacaoDeProtecao.Identificador))],
            new Dictionary<string, FatoDoProcesso>(),
            new Dictionary<string, string>());

        fatos.Identificadores("RG_NUMERO", emGrupo: true).Select(static c => c.GrupoCodigo).Should().Equal("COMPOSICAO_FAMILIAR");
        fatos.Identificadores("RG_NUMERO", emGrupo: false).Select(static c => c.GrupoCodigo).Should().Equal((string?)null);
    }

    [Fact(DisplayName = "O fato que não é identificador não é revelável")]
    public void Identificadores_FatoNaoIdentificador_Vazio()
    {
        FatosDaVersao fatos = new(
            new Dictionary<string, FatoDoProcesso> { ["NOME"] = Fato("NOME", ClassificacaoDeProtecao.Pessoal) },
            [],
            new Dictionary<string, FatoDoProcesso>(),
            new Dictionary<string, string>());

        fatos.Identificadores("NOME", emGrupo: false).Should().BeEmpty();
    }

    [Fact(DisplayName = "O nome vale pela classificação mais alta entre o civil e o social")]
    public void ClassificacaoDoNome_MaisAltaDosDois()
    {
        FatosDaVersao fatos = Nomes(("NOME", ClassificacaoDeProtecao.Pessoal), ("NOME_SOCIAL", ClassificacaoDeProtecao.Sensivel));

        fatos.ClassificacaoDoNome().Should().Be(ClassificacaoDeProtecao.Sensivel);
    }

    [Fact(DisplayName = "O nome sem classificação resolvida ou ausente da versão vale como sensível")]
    public void ClassificacaoDoNome_NaoResolvido_Sensivel()
    {
        Nomes(("NOME", ClassificacaoDeProtecao.Nenhuma)).ClassificacaoDoNome().Should().Be(ClassificacaoDeProtecao.Sensivel);
        Nomes().ClassificacaoDoNome().Should().Be(ClassificacaoDeProtecao.Sensivel);
    }

    private static FatosDaVersao Nomes(params (string Codigo, ClassificacaoDeProtecao Classificacao)[] nomes) => new(
        nomes.ToDictionary(static n => n.Codigo, static n => Fato(n.Codigo, n.Classificacao)),
        [],
        new Dictionary<string, FatoDoProcesso>(),
        new Dictionary<string, string>());

    private static GrupoDoProcesso Grupo(string codigo, FatoDoProcesso membro) =>
        new(codigo, codigo, null, 0, new Dictionary<string, FatoDoProcesso> { [membro.Codigo] = membro }, IncluiCandidato: false);

    private static FatoDoProcesso Fato(string codigo, ClassificacaoDeProtecao classificacao) => new(
        codigo, codigo, codigo, TiposDeItem.Texto, ComponentesDeItem.Padrao, classificacao, null, null, 0,
        new Dictionary<string, string>(), EhCpf: false);
}
