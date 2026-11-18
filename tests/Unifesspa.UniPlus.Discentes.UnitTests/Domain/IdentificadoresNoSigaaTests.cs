namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os identificadores que a origem atribui são sempre positivos; cada um é um conceito
/// à parte, com o código de erro do seu contexto.
/// </summary>
public sealed class IdentificadoresNoSigaaTests
{
    private static readonly (string Vo, string Codigo, string Mensagem)[] Identificadores =
    [
        (nameof(IdDoDiscenteNoSigaa), "VinculoDiscente.IdSigaaInvalido", "Id SIGAA deve ser positivo."),
        (nameof(IdDoCursoNoSigaa), "Curso.IdInvalido", "Id do curso deve ser positivo."),
        (nameof(IdDaUnidadeNoSigaa), "Curso.UnidadeIdInvalido", "Id da unidade deve ser positivo."),
        (nameof(IdDaSituacaoNoSigaa), "SituacaoAcademica.IdInvalido", "Id da situação deve ser positivo."),
    ];

    public static TheoryData<string> Nomes => [.. Identificadores.Select(i => i.Vo)];

    [Theory]
    [MemberData(nameof(Nomes))]
    public void Recusa_zero_e_negativo_com_o_codigo_e_a_mensagem_do_contexto(string vo)
    {
        (_, string codigo, string mensagem) = Identificadores.Single(i => i.Vo == vo);

        IEnumerable<DomainError?> erros = new long[] { 0, -1, int.MinValue }
            .Select(invalido => Criar(vo, invalido).Error);

        foreach (DomainError? erro in erros)
        {
            erro!.Code.Should().Be(codigo);
            erro.Message.Should().Be(mensagem);
        }
    }

    [Theory]
    [MemberData(nameof(Nomes))]
    public void Aceita_o_menor_positivo_e_o_maior_valor_do_tipo(string vo)
    {
        Criar(vo, 1).IsSuccess.Should().BeTrue();
        Criar(vo, int.MaxValue).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Identificador_do_discente_cabe_em_64_bits()
    {
        IdDoDiscenteNoSigaa.Criar(long.MaxValue).IsSuccess.Should().BeTrue();
        IdDoDiscenteNoSigaa.Criar(long.MinValue).Error!.Code.Should().Be("VinculoDiscente.IdSigaaInvalido");
    }

    [Fact]
    public void Unidade_recusa_primeiro_o_identificador_e_depois_o_nome()
    {
        UnidadeNoSigaa.Criar(0, " ").Error!.Code.Should().Be("Curso.UnidadeIdInvalido");
        UnidadeNoSigaa.Criar(1, " ").Error!.Code.Should().Be("Curso.UnidadeNomeVazio");
        UnidadeNoSigaa.Criar(64, " INSTITUTO ").Value.Should().Be(
            new UnidadeNoSigaa(IdDaUnidadeNoSigaa.Criar(64).Value!, NomeDaUnidade.Criar(" INSTITUTO ").Value!));
    }

    private static Result<long> Criar(string vo, long valor) => vo switch
    {
        nameof(IdDoDiscenteNoSigaa) => Como(IdDoDiscenteNoSigaa.Criar(valor), v => v.Valor),
        nameof(IdDoCursoNoSigaa) => Como(IdDoCursoNoSigaa.Criar((int)valor), v => v.Valor),
        nameof(IdDaUnidadeNoSigaa) => Como(IdDaUnidadeNoSigaa.Criar((int)valor), v => v.Valor),
        nameof(IdDaSituacaoNoSigaa) => Como(IdDaSituacaoNoSigaa.Criar((int)valor), v => v.Valor),
        _ => throw new ArgumentOutOfRangeException(nameof(vo), vo, "Identificador desconhecido."),
    };

    private static Result<long> Como<T>(Result<T> resultado, Func<T, long> valor) =>
        resultado.Match(criado => Result<long>.Success(valor(criado)), Result<long>.Failure);
}
