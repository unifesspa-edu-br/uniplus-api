namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o processo declara do cadastro de reserva por chamada (UNI-REQ-0354, UNI-REQ-0355): as chamadas
/// que têm cadastro de reserva, cada uma com a quantidade de pessoas da reserva convocadas por vaga. A
/// chamada sem declaração não tem reserva. A declaração é do próprio processo, indexada pelo número da
/// chamada, e não faz parte do quadro de vagas. Sem nenhuma chamada declarada, o processo não tem cadastro
/// de reserva, e a declaração é vazia, nunca nula.
/// </summary>
public sealed partial record CadastroDeReservaPorChamada
{
    private const string CampoDasChamadas = "cadastroDeReserva";

    private CadastroDeReservaPorChamada(IReadOnlyList<CadastroDeReservaDaChamada> chamadas) => Chamadas = chamadas;

    /// <summary>O processo sem cadastro de reserva em nenhuma chamada.</summary>
    public static CadastroDeReservaPorChamada SemReserva { get; } = new([]);

    /// <summary>As chamadas com cadastro de reserva, em ordem crescente do número da chamada.</summary>
    public IReadOnlyList<CadastroDeReservaDaChamada> Chamadas { get; }

    /// <summary>As declarações são iguais quando declaram as mesmas chamadas com as mesmas quantidades, qualquer que seja a ordem informada.</summary>
    public bool Equals(CadastroDeReservaPorChamada? outra) =>
        outra is not null && Chamadas.SequenceEqual(outra.Chamadas);

    public override int GetHashCode()
    {
        HashCode codigo = new();
        foreach (CadastroDeReservaDaChamada chamada in Chamadas)
        {
            codigo.Add(chamada);
        }

        return codigo.ToHashCode();
    }

    /// <summary>
    /// Confere a forma da declaração e acumula as recusas (ADR-0125): cada chamada informa um número a partir
    /// de 1, declarado uma só vez, e ao menos uma pessoa da reserva por vaga. A declaração sem chamada é a do
    /// processo sem cadastro de reserva. Quantas vagas cada chamada tem não é conferido aqui: vem do quadro,
    /// por quem convoca.
    /// </summary>
    public static Result<CadastroDeReservaPorChamada> Declarar(IReadOnlyList<CadastroDeReservaInformado?> chamadas)
    {
        ArgumentNullException.ThrowIfNull(chamadas);

        List<FieldError> recusas = [.. RecusasDeCadaChamada(chamadas)];
        return recusas.Count > 0
            ? Result<CadastroDeReservaPorChamada>.ValidationFailure(recusas)
            : Result<CadastroDeReservaPorChamada>.Success(new(
                [.. chamadas.Select(static chamada => new CadastroDeReservaDaChamada(
                        NumeroDaChamada.Criar(chamada!.NumeroDaChamada).Value!, chamada.PessoasDaReservaPorVaga!.Value))
                    .OrderBy(static chamada => chamada.Chamada.Valor)]));
    }

    private static IEnumerable<FieldError> RecusasDeCadaChamada(IReadOnlyList<CadastroDeReservaInformado?> chamadas)
    {
        HashSet<int> jaDeclaradas = [];
        for (int indice = 0; indice < chamadas.Count; indice++)
        {
            string campo = $"{CampoDasChamadas}[{indice}]";
            CadastroDeReservaInformado? chamada = chamadas[indice];
            foreach (FieldError recusa in RecusasDoNumero(chamada?.NumeroDaChamada, jaDeclaradas, $"{campo}.numeroDaChamada")
                .Concat(RecusasDaQuantidade(chamada?.PessoasDaReservaPorVaga, $"{campo}.pessoasDaReservaPorVaga")))
            {
                yield return recusa;
            }
        }
    }

    private static IEnumerable<FieldError> RecusasDoNumero(int? numero, HashSet<int> jaDeclaradas, string campo)
    {
        Result<NumeroDaChamada> chamada = NumeroDaChamada.Criar(numero);
        if (chamada.IsFailure)
        {
            yield return new FieldError(campo, chamada.Error!);
        }
        else if (!jaDeclaradas.Add(chamada.Value!.Valor))
        {
            yield return Recusa(
                campo,
                CadastroDeReservaErrorCodes.ChamadaRepetida,
                "Esta chamada já tem cadastro de reserva declarado; declare cada chamada uma só vez.");
        }
    }

    private static IEnumerable<FieldError> RecusasDaQuantidade(int? pessoasDaReservaPorVaga, string campo)
    {
        if (pessoasDaReservaPorVaga is not >= 1)
        {
            yield return Recusa(
                campo,
                CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida,
                "A chamada com cadastro de reserva convoca pelo menos uma pessoa da reserva por vaga; para não ter reserva, deixe a chamada sem declaração.");
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
