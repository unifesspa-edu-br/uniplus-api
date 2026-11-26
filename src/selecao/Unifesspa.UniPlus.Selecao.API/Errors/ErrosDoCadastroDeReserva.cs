namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

internal static class ErrosDoCadastroDeReserva
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new(CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.cadastro_de_reserva.numero_da_chamada_invalido", "Número da chamada inválido no cadastro de reserva")),
        new(CadastroDeReservaErrorCodes.ChamadaRepetida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.cadastro_de_reserva.chamada_repetida", "Chamada repetida no cadastro de reserva")),
        new(CadastroDeReservaErrorCodes.PessoasDaReservaPorVagaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.cadastro_de_reserva.pessoas_da_reserva_por_vaga_invalida", "Quantidade de pessoas da reserva por vaga inválida")),
    ];
}
