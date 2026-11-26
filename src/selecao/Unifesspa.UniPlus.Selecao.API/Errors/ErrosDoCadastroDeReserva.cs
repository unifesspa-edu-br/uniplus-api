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
        new(CadastroDeReservaErrorCodes.ParticipacaoAusente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.cadastro_de_reserva.participacao_ausente", "A participação da reserva é obrigatória na chamada com reserva")),
        new(CadastroDeReservaErrorCodes.ParticipacaoSemReserva, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.cadastro_de_reserva.participacao_com_quantidade_zero", "A chamada sem reserva não tem participação da reserva")),
        new(CadastroDeReservaErrorCodes.ParticipacaoDesconhecida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.cadastro_de_reserva.participacao_desconhecida", "Participação da reserva desconhecida")),
    ];
}
