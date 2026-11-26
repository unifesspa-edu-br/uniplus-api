namespace Unifesspa.UniPlus.Ingresso.TestSupport.Cenarios;

using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

public sealed class CopiaDaOfertaDeCursoDeTeste
{
    private readonly string _codigo = "ENGENHARIA_CIVIL_MARABA";

    public CopiaDaOfertaDeCurso Construir() => CopiaDaOfertaDeCurso.Criar(
        _codigo, "Engenharia Civil", "Bacharelado", "Unidade de Marabá", ["MATUTINO"]).Value ?? throw new InvalidOperationException("Não foi possível construir a oferta de teste.");
}
