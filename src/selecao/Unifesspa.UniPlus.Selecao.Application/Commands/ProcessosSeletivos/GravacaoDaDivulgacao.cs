namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Interfaces;

/// <summary>
/// O que a divulgação usa para registrar o certame: a linha de divulgação, a unidade de trabalho que a
/// grava e o relógio do registro.
/// </summary>
/// <param name="Certames">O repositório da linha de divulgação.</param>
/// <param name="UnitOfWork">A unidade de trabalho que grava a divulgação.</param>
/// <param name="Relogio">O relógio do registro.</param>
internal sealed record GravacaoDaDivulgacao(
    ICertameDivulgadoRepository Certames,
    ISelecaoUnitOfWork UnitOfWork,
    TimeProvider Relogio);
