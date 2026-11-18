namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os textos que a réplica guarda como o SIGAA mandou: o tamanho é medido sobre o texto
/// inalterado, nenhum espaço é aparado, e o valor persiste como veio.
/// </summary>
public sealed class TextosDaReplicaTests
{
    private static readonly (string Vo, int Limite, string Vazio, string Longo)[] Obrigatorios =
    [
        (nameof(NomeDoDiscente), 250, "VinculoDiscente.NomeVazio", "VinculoDiscente.NomeLongo"),
        (nameof(NivelDoVinculo), 5, "VinculoDiscente.NivelVazio", "VinculoDiscente.NivelLongo"),
        (nameof(NomeDoCurso), 250, "Curso.NomeVazio", "Curso.NomeLongo"),
        (nameof(NomeDaUnidade), 250, "Curso.UnidadeNomeVazio", "Curso.UnidadeNomeLongo"),
        (nameof(DescricaoDaSituacao), 250, "SituacaoAcademica.DescricaoVazia", "SituacaoAcademica.DescricaoLonga"),
    ];

    private static readonly (string Vo, int Limite, string Longo)[] Opcionais =
    [
        (nameof(CodigoEmecDoCursoNaReplica), 20, "Curso.CodigoEmecLongo"),
        (nameof(QualificadorDaSituacao), 100, "SituacaoAcademica.VinculoLongo"),
    ];

    public static TheoryData<string> NomesObrigatorios => [.. Obrigatorios.Select(o => o.Vo)];

    public static TheoryData<string> NomesOpcionais => [.. Opcionais.Select(o => o.Vo)];

    [Theory]
    [MemberData(nameof(NomesObrigatorios))]
    public void Obrigatorio_recusa_o_texto_ausente_ou_em_branco(string vo)
    {
        (_, int limite, string codigo, _) = Obrigatorios.Single(o => o.Vo == vo);

        Criar(vo, null).Codigo.Should().Be(codigo);
        Criar(vo, "").Codigo.Should().Be(codigo);
        Criar(vo, "   ").Codigo.Should().Be(codigo);
        Criar(vo, new string(' ', limite + 1)).Codigo.Should().Be(codigo, "obrigatório vem antes do tamanho");
    }

    [Theory]
    [MemberData(nameof(NomesObrigatorios))]
    public void Obrigatorio_aceita_o_limite_e_recusa_um_a_mais(string vo)
    {
        (_, int limite, _, string codigo) = Obrigatorios.Single(o => o.Vo == vo);

        Criar(vo, Texto(limite)).Codigo.Should().BeNull();
        Criar(vo, Texto(limite + 1)).Codigo.Should().Be(codigo);
        Criar(vo, " " + Texto(limite)).Codigo.Should().Be(codigo, "o espaço conta no limite");
    }

    [Theory]
    [MemberData(nameof(NomesObrigatorios))]
    public void Obrigatorio_guarda_o_texto_como_veio_sem_aparar(string vo)
    {
        int limite = Obrigatorios.Single(o => o.Vo == vo).Limite;

        Criar(vo, " x ").Valor.Should().Be(" x ");
        Criar(vo, " " + Texto(limite - 1)).Valor.Should().Be(" " + Texto(limite - 1));
    }

    [Theory]
    [MemberData(nameof(NomesOpcionais))]
    public void Opcional_ausente_e_aceito_e_o_resto_e_guardado_como_veio(string vo)
    {
        Criar(vo, null).Should().Be(new Desfecho(null, null));
        Criar(vo, "").Should().Be(new Desfecho("", null));
        Criar(vo, "  ").Valor.Should().Be("  ");
        Criar(vo, " x ").Valor.Should().Be(" x ");
    }

    [Theory]
    [MemberData(nameof(NomesOpcionais))]
    public void Opcional_aceita_o_limite_e_recusa_um_a_mais(string vo)
    {
        (_, int limite, string codigo) = Opcionais.Single(o => o.Vo == vo);

        Criar(vo, Texto(limite)).Codigo.Should().BeNull();
        Criar(vo, Texto(limite + 1)).Codigo.Should().Be(codigo);
        Criar(vo, " " + Texto(limite)).Codigo.Should().Be(codigo, "o espaço conta no limite");
    }

    private static string Texto(int tamanho) => new('A', tamanho);

    private static Desfecho Criar(string vo, string? texto) => vo switch
    {
        nameof(NomeDoDiscente) => De(NomeDoDiscente.Criar(texto), v => v.Valor),
        nameof(NivelDoVinculo) => De(NivelDoVinculo.Criar(texto), v => v.Valor),
        nameof(NomeDoCurso) => De(NomeDoCurso.Criar(texto), v => v.Valor),
        nameof(NomeDaUnidade) => De(NomeDaUnidade.Criar(texto), v => v.Valor),
        nameof(DescricaoDaSituacao) => De(DescricaoDaSituacao.Criar(texto), v => v.Valor),
        nameof(CodigoEmecDoCursoNaReplica) => De(CodigoEmecDoCursoNaReplica.Criar(texto), v => v?.Valor),
        nameof(QualificadorDaSituacao) => De(QualificadorDaSituacao.Criar(texto), v => v?.Valor),
        _ => throw new ArgumentOutOfRangeException(nameof(vo), vo, "Texto da réplica desconhecido."),
    };

    private static Desfecho De<T>(Result<T> resultado, Func<T, string?> valor) =>
        resultado.Match(
            criado => new Desfecho(valor(criado), null),
            erro => new Desfecho(null, erro.Code));

    private readonly record struct Desfecho(string? Valor, string? Codigo);
}
