namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A recusa que um decisor tenta na etapa corrente do pedido, com o motivo já normalizado.</summary>
internal sealed record RecusaNaEtapa(Identidade Decisor, EtapaDoPedido Etapa, string? Motivo, PerfilAvaliado Perfil, DateTimeOffset Instante)
{
    internal AtoDoAgente Ato => new(Decisor, Instante);

    internal List<FieldError> Recusas(SegregacaoDoPedido segregacao)
    {
        List<FieldError> erros = [];
        segregacao.Conferir(Decisor, Etapa, erros);

        if (Motivo is null)
        {
            erros.Add(new("motivo", new DomainError(
                PedidoErrorCodes.MotivoInvalido,
                $"O motivo da recusa é obrigatório e deve ter até {MotivosDaVersao.MaxLength} caracteres.")));
        }

        return erros;
    }
}
