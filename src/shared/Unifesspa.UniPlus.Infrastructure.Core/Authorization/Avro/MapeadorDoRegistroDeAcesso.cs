namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;

using unifesspa.uniplus.autorizacao.registrodeacesso;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>Converte os registros de acesso decididos no valor Avro da mensagem, sem mudar nenhum conteúdo.</summary>
internal static class MapeadorDoRegistroDeAcesso
{
    public static LoteDoRegistroDeAcesso ParaLote(IReadOnlyList<RegistroDecisaoAcesso> registros)
    {
        ArgumentNullException.ThrowIfNull(registros);

        return new LoteDoRegistroDeAcesso { Registros = [.. registros.Select(ParaEntrada)] };
    }

    private static EntradaDoLote ParaEntrada(RegistroDecisaoAcesso registro) =>
        new() { Chave = registro.Chave, Registro = ParaRegistro(registro) };

    private static RegistroDeAcesso ParaRegistro(RegistroDecisaoAcesso origem)
    {
        RegistroDeAcesso registro = ParaAcesso(origem);
        AplicarResource(registro, origem);
        AplicarDesfecho(registro, origem);

        return registro;
    }

    private static RegistroDeAcesso ParaAcesso(RegistroDecisaoAcesso origem) =>
        new()
        {
            Instante = origem.Instante.UtcDateTime,
            AtorEmissor = origem.AtorEmissor,
            AtorContaId = origem.AtorContaId,
            RequestId = origem.RequestId,
            ExecucaoId = Texto(origem.ExecucaoId),
            Permissao = origem.Permissao,
            Origem = origem.Origem,
            ClienteOidc = origem.ClienteOidc,
        };

    private static void AplicarResource(RegistroDeAcesso registro, RegistroDecisaoAcesso origem)
    {
        registro.ResourceTipo = origem.ResourceTipo;
        registro.Sensibilidade = origem.Sensibilidade;
        registro.UnidadeProprietariaId = Texto(origem.UnidadeProprietariaId);
        registro.ProcessoId = Texto(origem.ProcessoId);
        registro.ChamadaId = Texto(origem.ChamadaId);
        registro.ResourceId = Texto(origem.ResourceId);
        registro.InscricaoId = Texto(origem.InscricaoId);
        registro.FatoCodigo = origem.FatoCodigo;
        registro.Ocorrencia = origem.Ocorrencia;
    }

    private static void AplicarDesfecho(RegistroDeAcesso registro, RegistroDecisaoAcesso origem)
    {
        registro.Resultado = ValoresCanonicos.De(origem.Resultado);
        registro.Permitido = origem.Permitido;
        registro.MotivoNegativa = origem.MotivoNegativa;
        registro.FonteGrant = origem.FonteGrant;
        registro.GrantId = Texto(origem.GrantId);
        registro.PerfilId = Texto(origem.PerfilId);
        registro.VersaoId = Texto(origem.VersaoId);
        registro.EscopoUnidadeId = Texto(origem.EscopoUnidadeId);
    }

    private static string Texto(Guid identificador) => identificador.ToString("D");

    private static string? Texto(Guid? identificador) => identificador?.ToString("D");
}
