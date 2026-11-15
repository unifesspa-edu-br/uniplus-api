namespace unifesspa.uniplus.autorizacao.registrodeacesso;

using global::Avro;
using global::Avro.Specific;

using Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;

/// <summary>
/// O que ficou gravado sobre um uso de permissão, na forma Avro. Os identificadores vão como texto de UUID, e os
/// valores de vocabulário (sensibilidade, resultado, motivo, fonte) como o valor canônico, em texto: motivo novo no
/// vocabulário não muda o esquema. A ordem dos campos é a do esquema.
/// </summary>
public sealed class RegistroDeAcesso : ISpecificRecord
{
    private static readonly Func<RegistroDeAcesso, object?>[] Leitores =
    [
        static registro => registro.Instante,
        static registro => registro.AtorEmissor,
        static registro => registro.AtorContaId,
        static registro => registro.RequestId,
        static registro => registro.ExecucaoId,
        static registro => registro.Permissao,
        static registro => registro.ResourceTipo,
        static registro => registro.Sensibilidade,
        static registro => registro.UnidadeProprietariaId,
        static registro => registro.ProcessoId,
        static registro => registro.ChamadaId,
        static registro => registro.ResourceId,
        static registro => registro.InscricaoId,
        static registro => registro.FatoCodigo,
        static registro => registro.Ocorrencia,
        static registro => registro.Origem,
        static registro => registro.ClienteOidc,
        static registro => registro.Resultado,
        static registro => registro.Permitido,
        static registro => registro.MotivoNegativa,
        static registro => registro.FonteGrant,
        static registro => registro.GrantId,
        static registro => registro.PerfilId,
        static registro => registro.VersaoId,
        static registro => registro.EscopoUnidadeId,
    ];

    private static readonly Action<RegistroDeAcesso, object?>[] Escritores =
    [
        static (registro, valor) => registro.Instante = (DateTime)valor!,
        static (registro, valor) => registro.AtorEmissor = (string)valor!,
        static (registro, valor) => registro.AtorContaId = (string)valor!,
        static (registro, valor) => registro.RequestId = (string)valor!,
        static (registro, valor) => registro.ExecucaoId = (string)valor!,
        static (registro, valor) => registro.Permissao = (string)valor!,
        static (registro, valor) => registro.ResourceTipo = (string)valor!,
        static (registro, valor) => registro.Sensibilidade = (string)valor!,
        static (registro, valor) => registro.UnidadeProprietariaId = (string?)valor,
        static (registro, valor) => registro.ProcessoId = (string?)valor,
        static (registro, valor) => registro.ChamadaId = (string?)valor,
        static (registro, valor) => registro.ResourceId = (string?)valor,
        static (registro, valor) => registro.InscricaoId = (string?)valor,
        static (registro, valor) => registro.FatoCodigo = (string?)valor,
        static (registro, valor) => registro.Ocorrencia = (string?)valor,
        static (registro, valor) => registro.Origem = (string)valor!,
        static (registro, valor) => registro.ClienteOidc = (string?)valor,
        static (registro, valor) => registro.Resultado = (string)valor!,
        static (registro, valor) => registro.Permitido = (bool)valor!,
        static (registro, valor) => registro.MotivoNegativa = (string?)valor,
        static (registro, valor) => registro.FonteGrant = (string?)valor,
        static (registro, valor) => registro.GrantId = (string?)valor,
        static (registro, valor) => registro.PerfilId = (string?)valor,
        static (registro, valor) => registro.VersaoId = (string?)valor,
        static (registro, valor) => registro.EscopoUnidadeId = (string?)valor,
    ];

    /// <summary>Instante do acesso, em UTC, na precisão de microssegundos.</summary>
    public DateTime Instante { get; set; }

    /// <summary>Emissor do token de quem acessou.</summary>
    public string AtorEmissor { get; set; } = string.Empty;

    /// <summary>Identificador da conta de quem acessou, opaco e atribuído pelo emissor.</summary>
    public string AtorContaId { get; set; } = string.Empty;

    /// <summary>Correlação enviada pelo cliente.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Execução do servidor, a mesma para todo acesso da mesma requisição.</summary>
    public string ExecucaoId { get; set; } = string.Empty;

    /// <summary>Código da permissão exigida.</summary>
    public string Permissao { get; set; } = string.Empty;

    /// <summary>Tipo do resource alvo.</summary>
    public string ResourceTipo { get; set; } = string.Empty;

    /// <summary>Sensibilidade efetiva do dado alcançado, em valor canônico.</summary>
    public string Sensibilidade { get; set; } = string.Empty;

    /// <summary>Unidade proprietária do resource.</summary>
    public string? UnidadeProprietariaId { get; set; }

    /// <summary>Processo seletivo do resource.</summary>
    public string? ProcessoId { get; set; }

    /// <summary>Chamada do resource.</summary>
    public string? ChamadaId { get; set; }

    /// <summary>Registro alvo da operação.</summary>
    public string? ResourceId { get; set; }

    /// <summary>Inscrição a que o acesso se refere.</summary>
    public string? InscricaoId { get; set; }

    /// <summary>Código do fato revelado, nunca o valor.</summary>
    public string? FatoCodigo { get; set; }

    /// <summary>Ocorrência do fato revelado num grupo que se repete.</summary>
    public string? Ocorrencia { get; set; }

    /// <summary>Canal pelo qual a requisição chegou.</summary>
    public string Origem { get; set; } = string.Empty;

    /// <summary>Client OIDC que obteve o token do acesso.</summary>
    public string? ClienteOidc { get; set; }

    /// <summary>Desfecho da decisão, em valor canônico.</summary>
    public string Resultado { get; set; } = string.Empty;

    /// <summary>Veredito derivado do resultado.</summary>
    public bool Permitido { get; set; }

    /// <summary>Motivo canônico da negativa.</summary>
    public string? MotivoNegativa { get; set; }

    /// <summary>Fonte canônica do acesso permitido.</summary>
    public string? FonteGrant { get; set; }

    /// <summary>Concessão usada.</summary>
    public string? GrantId { get; set; }

    /// <summary>Perfil de acesso da concessão usada.</summary>
    public string? PerfilId { get; set; }

    /// <summary>Versão do perfil a que a concessão está presa.</summary>
    public string? VersaoId { get; set; }

    /// <summary>Unidade do escopo da concessão usada.</summary>
    public string? EscopoUnidadeId { get; set; }

    /// <inheritdoc />
    public Schema Schema => EsquemaDoRegistroDeAcesso.Registro;

    /// <inheritdoc />
    public object Get(int fieldPos) => PosicaoValida(fieldPos)
        ? Leitores[fieldPos](this)!
        : throw new AvroRuntimeException($"Posição de campo inválida em RegistroDeAcesso.Get: {fieldPos}");

    /// <inheritdoc />
    public void Put(int fieldPos, object fieldValue)
    {
        if (!PosicaoValida(fieldPos))
        {
            throw new AvroRuntimeException($"Posição de campo inválida em RegistroDeAcesso.Put: {fieldPos}");
        }

        Escritores[fieldPos](this, fieldValue);
    }

    private static bool PosicaoValida(int posicao) =>
        posicao >= 0 && posicao < Leitores.Length;
}
