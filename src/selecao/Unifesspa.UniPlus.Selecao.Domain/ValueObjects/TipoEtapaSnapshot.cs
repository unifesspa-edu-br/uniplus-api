namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cópia por valor do tipo de etapa resolvido em Configuração no momento da
/// definição. A etapa nunca relê a configuração de tipos para mudar a própria identidade.
/// </summary>
public sealed record TipoEtapaSnapshot
{
    private const char CaractereNulo = (char)0;

    private TipoEtapaSnapshot() { }

    private TipoEtapaSnapshot(Guid origemId, string codigo, string nome)
    {
        OrigemId = origemId;
        Codigo = codigo;
        Nome = nome;
    }

    /// <remarks>Usado apenas na construção; a identidade é persistida no próprio snapshot.</remarks>
    public Guid OrigemId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;

    public static Result<TipoEtapaSnapshot> Criar(Guid origemId, string codigo, string nome)
    {
        if (origemId == Guid.Empty)
        {
            return Falha("TipoEtapaSnapshot.OrigemIdObrigatorio", "Origem do tipo de etapa é obrigatória.");
        }
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Falha("TipoEtapaSnapshot.CodigoObrigatorio", "Código do tipo de etapa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha("TipoEtapaSnapshot.NomeObrigatorio", "Nome do tipo de etapa é obrigatório.");
        }

        // NFC na fronteira de congelamento (mesma normalização do payload canônico,
        // HashCanonicalComputer.NormalizeNfc): sem isso, o mesmo código digitado em forma
        // decomposta aqui e recomposta (NFC) ao serializar no envelope vira dois valores
        // ordinalmente diferentes depois de um ciclo de retificação descartada — o código
        // congelado muda de representação sem que o dado mude de significado, e
        // AvaliadorConformidadeLegal, que compara por igualdade ordinal, passa a reportar a
        // etapa como ausente mesmo com a regra e a etapa usando o "mesmo" código.
        string codigoNormalizado = codigo.Trim().Normalize(NormalizationForm.FormC);
        string nomeNormalizado = nome.Trim().Normalize(NormalizationForm.FormC);

        // Defesa de decode: um envelope adulterado não pode injetar U+0000 e só falhar
        // depois, na constraint do Postgres — o VO recusa aqui, na fronteira do domínio.
        if (codigoNormalizado.Contains(CaractereNulo) || nomeNormalizado.Contains(CaractereNulo))
        {
            return Falha("TipoEtapaSnapshot.CaractereNulo", "Snapshot do tipo de etapa não pode conter o caractere nulo (U+0000).");
        }

        if (codigoNormalizado.Length > 64 || nomeNormalizado.Length > 200)
        {
            return Falha("TipoEtapaSnapshot.TamanhoInvalido", "Snapshot do tipo de etapa excede o tamanho permitido.");
        }

        return Result<TipoEtapaSnapshot>.Success(new TipoEtapaSnapshot(origemId, codigoNormalizado, nomeNormalizado));
    }

    public override string ToString() => Codigo;

    private static Result<TipoEtapaSnapshot> Falha(string code, string message) =>
        Result<TipoEtapaSnapshot>.Failure(new DomainError(code, message));
}
