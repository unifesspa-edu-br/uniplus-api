namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o processo declara do cadastro de reserva por chamada (UNI-REQ-0354, UNI-REQ-0355): as chamadas
/// declaradas, cada uma com a quantidade de pessoas da reserva convocadas por vaga, que pode ser zero. A
/// chamada sem declaração não tem reserva. A declaração é do próprio processo, indexada pelo número da
/// chamada, e não faz parte do quadro de vagas. Sem nenhuma chamada declarada, o processo não tem cadastro
/// de reserva, e a declaração é vazia, nunca nula.
/// </summary>
public sealed record CadastroDeReservaPorChamada
{
    private const string CampoDasChamadas = "cadastroDeReserva";

    private CadastroDeReservaPorChamada(IReadOnlyList<CadastroDeReservaDaChamada> chamadas) => Chamadas = chamadas;

    /// <summary>O processo sem cadastro de reserva em nenhuma chamada.</summary>
    public static CadastroDeReservaPorChamada SemReserva { get; } = new([]);

    /// <summary>As chamadas declaradas, em ordem crescente do número da chamada.</summary>
    public IReadOnlyList<CadastroDeReservaDaChamada> Chamadas { get; }

    /// <summary>As declarações são iguais quando declaram as mesmas chamadas com as mesmas quantidades, qualquer que seja a ordem informada.</summary>
    public bool Equals(CadastroDeReservaPorChamada? outra) =>
        outra is not null && Chamadas.SequenceEqual(outra.Chamadas);

    /// <summary>
    /// O máximo de pessoas do cadastro de reserva que a chamada convoca: as vagas da chamada vezes as pessoas da
    /// reserva por vaga declaradas para ela, ou zero na chamada sem declaração. As vagas vêm de quem convoca, e
    /// a declaração nunca lê o quadro. Reduzir o máximo às pessoas elegíveis é de quem convoca.
    /// </summary>
    /// <param name="chamada">A chamada que convoca.</param>
    /// <param name="vagasDaChamada">As vagas que a chamada tem, não negativas.</param>
    public long PessoasDaReservaConvocadas(NumeroDaChamada chamada, int vagasDaChamada)
    {
        ArgumentNullException.ThrowIfNull(chamada);
        ArgumentOutOfRangeException.ThrowIfNegative(vagasDaChamada);

        CadastroDeReservaDaChamada? declarada = Chamadas.FirstOrDefault(cadastro => cadastro.Chamada == chamada);
        return declarada is null ? 0 : (long)vagasDaChamada * declarada.PessoasDaReservaPorVaga;
    }

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
    /// de 1, declarado uma só vez, a quantidade de pessoas da reserva por vaga, não negativa, e a participação da
    /// reserva (UNI-REQ-0505), que a quantidade maior que zero exige e a quantidade zero recusa. A declaração sem
    /// chamada é a do processo sem cadastro de reserva. Quantas vagas cada chamada tem não é conferido aqui: vem do quadro,
    /// por quem convoca.
    /// </summary>
    public static Result<CadastroDeReservaPorChamada> Declarar(IReadOnlyList<CadastroDeReservaInformado?> chamadas)
    {
        ArgumentNullException.ThrowIfNull(chamadas);

        List<FieldError> recusas = [.. RecusasDeCadaChamada(chamadas)];
        return recusas.Count > 0
            ? Result<CadastroDeReservaPorChamada>.ValidationFailure(recusas)
            : Result<CadastroDeReservaPorChamada>.Success(DeItensConferidos(chamadas.Select(static chamada => Conferida(chamada!))));
    }

    /// <summary>A declaração dos itens que já passaram pela conferência de <see cref="Declarar"/>, em ordem crescente do número da chamada.</summary>
    internal static CadastroDeReservaPorChamada DeItensConferidos(IEnumerable<CadastroDeReservaDaChamada> itens)
    {
        CadastroDeReservaDaChamada[] ordenados = [.. itens.OrderBy(static item => item.Chamada.Valor)];
        return ordenados.Length == 0 ? SemReserva : new(ordenados);
    }

    private static CadastroDeReservaDaChamada Conferida(CadastroDeReservaInformado chamada) => new(
        NumeroDaChamada.Criar(chamada.NumeroDaChamada).Value!,
        chamada.PessoasDaReservaPorVaga!.Value,
        CatalogoDe<ParticipacaoDaReserva>.TentarObter(chamada.ParticipacaoDaReserva, out ParticipacaoDaReserva participacao) ? participacao : null);

    private static IEnumerable<FieldError> RecusasDeCadaChamada(IReadOnlyList<CadastroDeReservaInformado?> chamadas)
    {
        HashSet<int> jaDeclaradas = [];
        for (int indice = 0; indice < chamadas.Count; indice++)
        {
            string campo = $"{CampoDasChamadas}[{indice}]";
            CadastroDeReservaInformado? chamada = chamadas[indice];
            foreach (FieldError recusa in RecusasDoNumero(chamada?.NumeroDaChamada, jaDeclaradas, $"{campo}.numeroDaChamada")
                .Concat(RecusasDaQuantidade(chamada?.PessoasDaReservaPorVaga, $"{campo}.pessoasDaReservaPorVaga"))
                .Concat(RecusasDaParticipacao(chamada, $"{campo}.participacaoDaReserva")))
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
        if (pessoasDaReservaPorVaga is not >= 0)
        {
            yield return Recusa(
                campo,
                CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida,
                "Informe quantas pessoas da reserva a chamada convoca por vaga, um inteiro a partir de 0; zero é a chamada sem reserva.");
        }
    }

    private static IEnumerable<FieldError> RecusasDaParticipacao(CadastroDeReservaInformado? chamada, string campo)
    {
        string? codigo = chamada?.ParticipacaoDaReserva;
        if (codigo is not null && !CatalogoDe<ParticipacaoDaReserva>.TentarObter(codigo, out _))
        {
            yield return Recusa(campo, CadastroDeReservaErrorCodes.ParticipacaoDesconhecida, $"A participação da reserva informada não existe; escolha {OpcoesDeParticipacao()}.");
        }
        else if (chamada?.PessoasDaReservaPorVaga is > 0 && codigo is null)
        {
            yield return Recusa(campo, CadastroDeReservaErrorCodes.ParticipacaoAusente, $"A chamada com pessoas da reserva por vaga precisa da participação da reserva; escolha {OpcoesDeParticipacao()}.");
        }
        else if (chamada?.PessoasDaReservaPorVaga is 0 && codigo is not null)
        {
            yield return Recusa(campo, CadastroDeReservaErrorCodes.ParticipacaoSemReserva, "A chamada sem pessoas da reserva por vaga não tem opção de participação a escolher; retire a opção ou informe a quantidade.");
        }
    }

    private static string OpcoesDeParticipacao() =>
        string.Join(" ou ", CatalogoDe<ParticipacaoDaReserva>.Todos().Select(static opcao => $"\"{opcao.Rotulo}\""));

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
