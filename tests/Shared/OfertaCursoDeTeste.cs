namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que nomeia uma oferta de curso para o candidato, para a distribuição de vagas dos testes.</summary>
internal static class OfertaCursoDeTeste
{
    /// <summary>Uma instância nova a cada leitura: o EF não aceita o mesmo valor owned em duas distribuições.</summary>
    public static CopiaDaOfertaDeCurso Medicina =>
        CopiaDaOfertaDeCurso.Criar("MEDICINA_MARABA", "Medicina", "Bacharelado", "Campus Universitário de Marabá", ["MATUTINO", "VESPERTINO"]).Value!;

    public static CopiaDaOfertaDeCurso Pedagogia =>
        CopiaDaOfertaDeCurso.Criar("PEDAGOGIA_MARABA", "Pedagogia", "Licenciatura", "Campus Universitário de Marabá", ["NOTURNO"]).Value!;
}
