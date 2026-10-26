namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>
/// A definição versionada de um arquivo trocado com o INEP. Código e versão a nomeiam; o hash da
/// definição a identifica, de modo que duas definições com o mesmo nome mas conteúdo diferente
/// nunca passem por iguais.
/// </summary>
public interface ILeiauteDeArquivoDoInep
{
    string Codigo { get; }

    string Versao { get; }

    TipoDeLeiauteDoInep Tipo { get; }

    string Descricao { get; }

    /// <summary>Leiaute desativado sai da escolha do administrador, mas o lote que o usou continua a citá-lo.</summary>
    bool Ativo { get; }

    /// <summary>Quantas colunas cada linha do arquivo tem.</summary>
    int QuantidadeDeCampos { get; }

    /// <summary>SHA-256, em hexadecimal minúsculo, da definição canônica do leiaute.</summary>
    string Hash { get; }
}
