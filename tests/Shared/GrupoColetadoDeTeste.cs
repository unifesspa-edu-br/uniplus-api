namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Monta o grupo coletado dos testes: sem dizer nada além do código e da ordem, é o grupo "Composição
/// familiar" de zero a cinco ocorrências, na seção de dados do formulário, sempre exibido e com
/// obrigatoriedade <c>Nunca</c>. Cada método devolve um construtor novo, então um ponto de partida
/// compartilhado não vaza para os demais.
/// </summary>
internal sealed record GrupoColetadoDeTeste
{
    private string Codigo { get; init; } = string.Empty;

    private int Ordem { get; init; }

    private string? Secao { get; init; } = FormularioDeTeste.Secao;

    private string Rotulo { get; init; } = "Composição familiar";

    private int Minimo { get; init; }

    private int? Maximo { get; init; } = 5;

    private bool IncluiCandidato { get; init; }

    private PredicadoDnf? Exibicao { get; init; }

    private Obrigatoriedade Obrigatoriedade { get; init; } = Obrigatoriedade.Nunca;

    private FinalidadeFormulario Finalidade { get; init; }

    private IReadOnlyList<FatoColetado> Campos { get; init; } = [];

    /// <summary>O grupo do código na ordem dada, sem campos até o teste dá-los.</summary>
    public static GrupoColetadoDeTeste Agrupando(string codigo, int ordem) => new() { Codigo = codigo, Ordem = ordem };

    /// <summary>O grupo igual ao dado, para o teste que o move de ordem sem refazê-lo.</summary>
    public static GrupoColetadoDeTeste Como(GrupoColetado grupo) =>
        Agrupando(grupo.Codigo, grupo.Ordem) with
        {
            Secao = grupo.EtapaCodigo,
            Rotulo = grupo.Rotulo,
            Minimo = grupo.Minimo,
            Maximo = grupo.Maximo,
            IncluiCandidato = grupo.IncluiCandidato,
            Exibicao = grupo.Exibicao,
            Obrigatoriedade = grupo.Obrigatoriedade,
            Finalidade = grupo.Finalidade,
            Campos = [.. grupo.Subitens],
        };

    public GrupoColetadoDeTeste ComCampos(params IReadOnlyList<FatoColetado> campos) => this with { Campos = campos };

    public GrupoColetadoDeTeste NaSecao(string? secao) => this with { Secao = secao };

    public GrupoColetadoDeTeste NaOrdem(int ordem) => this with { Ordem = ordem };

    public GrupoColetadoDeTeste ComRotulo(string rotulo) => this with { Rotulo = rotulo };

    public GrupoColetadoDeTeste ComOcorrencias(int minimo, int? maximo) => this with { Minimo = minimo, Maximo = maximo };

    public GrupoColetadoDeTeste IncluindoOCandidato(bool inclui = true) => this with { IncluiCandidato = inclui };

    public GrupoColetadoDeTeste ExibidoQuando(PredicadoDnf? exibicao) => this with { Exibicao = exibicao };

    public GrupoColetadoDeTeste ComObrigatoriedade(Obrigatoriedade obrigatoriedade) => this with { Obrigatoriedade = obrigatoriedade };

    /// <summary>A finalidade do formulário do grupo; os campos de outra finalidade são refeitos nela.</summary>
    public GrupoColetadoDeTeste DaFinalidade(FinalidadeFormulario finalidade) => this with { Finalidade = finalidade };

    /// <summary>Cria o grupo pela fábrica, para o teste que olha a recusa.</summary>
    public Result<GrupoColetado> Criar() =>
        GrupoColetado.Criar(
            Codigo,
            Ordem,
            Secao,
            Rotulo,
            new OcorrenciasDoGrupo(Minimo, Maximo, IncluiCandidato),
            new ExibicaoEObrigatoriedade(Exibicao, Obrigatoriedade),
            CamposNaFinalidade());

    /// <summary>O grupo criado, para o teste que parte de um grupo válido.</summary>
    public GrupoColetado Construir() => Criar().Value!;

    private FatoColetado[] CamposNaFinalidade() =>
        [.. Campos.Select(campo => Finalidade == FinalidadeFormulario.Nenhuma || campo.Finalidade == Finalidade
            ? campo
            : FatoColetadoDeTeste.Como(campo).DaFinalidade(Finalidade).Construir())];
}
