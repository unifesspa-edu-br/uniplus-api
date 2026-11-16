namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>A aprovação que um decisor tenta na etapa corrente do pedido, com o perfil que ele avaliou.</summary>
internal sealed record AprovacaoNaEtapa(
    Identidade Decisor,
    EtapaDoPedido Etapa,
    PerfilAvaliado Perfil,
    ContextoDaAprovacao Contexto,
    DateTimeOffset Instante,
    DateTimeOffset? ValidaAte);
