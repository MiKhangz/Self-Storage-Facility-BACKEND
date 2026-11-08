using System.Globalization;
using System.Text.RegularExpressions;

namespace StorEase.Application.Common;

/// <summary>
/// Puts an API error into the language of the request. Services write their messages in English and that text is the
/// key, as on the frontend; {0}, {1} stand for the values in a message, and a value that is a status is translated too.
/// A message missing here goes out in English; ErrorTextTests fails if the code has one.
/// </summary>
public static class ErrorText
{
    public static string Localize(string message)
    {
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "vi")
            return message;
        if (Vietnamese.TryGetValue(message, out var exact))
            return exact;
        foreach (var (pattern, text) in Templates)
        {
            var match = pattern.Match(message);
            if (match.Success)
                return Hole.Replace(text, h =>
                {
                    var value = match.Groups[h.Groups[1].Value[0] - '0' + 1].Value;
                    return Statuses.GetValueOrDefault(value, value);
                });
        }
        return message;
    }

    private static readonly Regex Hole = new(@"\{(\d)\}");

    private static readonly Dictionary<string, string> Statuses = new()
    {
        ["Draft"] = "nháp", ["Active"] = "đang hiệu lực", ["PendingReturn"] = "đang trả kho", ["Closed"] = "đã đóng", ["Overdue"] = "quá hạn",
        ["Suspended"] = "tạm khóa", ["Terminated"] = "đã chấm dứt", ["Issued"] = "đã lập", ["Paid"] = "đã trả", ["WrittenOff"] = "đã xóa nợ",
        ["Open"] = "đang mở", ["Scheduled"] = "đã lên lịch", ["InProgress"] = "đang xử lý", ["Done"] = "đã xong", ["Cancelled"] = "đã hủy",
        ["Reminder"] = "nhắc nhở", ["LateFee"] = "phí trễ hạn", ["ClearOut"] = "dọn kho", ["Resolved"] = "đã giải quyết", ["Pending"] = "đang chờ",
        ["Succeeded"] = "thành công", ["Failed"] = "thất bại", ["Refunded"] = "đã hoàn", ["Superseded"] = "đã thay thế", ["Confirmed"] = "đã xác nhận",
        ["HandedOver"] = "đã bàn giao", ["Rejected"] = "bị từ chối", ["Expired"] = "hết hạn", ["Requested"] = "đã yêu cầu", ["Approved"] = "đã duyệt",
        ["Inspected"] = "đã kiểm tra", ["Completed"] = "hoàn thành", ["New"] = "mới", ["WaitingForCustomer"] = "chờ khách phản hồi", ["ToDo"] = "cần làm",
        ["Available"] = "còn trống", ["Reserved"] = "đã giữ chỗ", ["Occupied"] = "đang thuê", ["Maintenance"] = "bảo trì", ["Retired"] = "ngừng sử dụng",
    };

    private static readonly Dictionary<string, string> Vietnamese = new()
    {
        ["An unexpected error occurred."] = "Đã có lỗi xảy ra. Vui lòng thử lại.",

        // Not found
        ["Access code not found."] = "Không tìm thấy mã ra vào.",
        ["Account not found."] = "Không tìm thấy tài khoản.",
        ["Assignee not found."] = "Không tìm thấy người được giao.",
        ["Contract not found."] = "Không tìm thấy hợp đồng.",
        ["Customer not found."] = "Không tìm thấy khách hàng.",
        ["Deposit refund not found."] = "Không tìm thấy khoản hoàn cọc.",
        ["Facility not found."] = "Không tìm thấy cơ sở.",
        ["Fee type not found."] = "Không tìm thấy loại phí.",
        ["Floor not found."] = "Không tìm thấy tầng.",
        ["Handover not found."] = "Không tìm thấy biên bản bàn giao.",
        ["Invoice not found."] = "Không tìm thấy hóa đơn.",
        ["Maintenance order not found."] = "Không tìm thấy lệnh bảo trì.",
        ["One or more facilities were not found."] = "Không tìm thấy một hoặc nhiều cơ sở.",
        ["Overdue case not found."] = "Không tìm thấy hồ sơ quá hạn.",
        ["Payment not found."] = "Không tìm thấy thanh toán.",
        ["Photo not found."] = "Không tìm thấy ảnh.",
        ["Pricing policy not found."] = "Không tìm thấy chính sách giá.",
        ["Renewal not found."] = "Không tìm thấy yêu cầu gia hạn.",
        ["Reservation not found."] = "Không tìm thấy đơn đặt kho.",
        ["Return request not found."] = "Không tìm thấy yêu cầu trả kho.",
        ["Shift not found."] = "Không tìm thấy ca làm.",
        ["Staff account not found."] = "Không tìm thấy tài khoản nhân viên.",
        ["Staff member not found."] = "Không tìm thấy nhân viên.",
        ["Storage unit not found."] = "Không tìm thấy kho.",
        ["Task not found."] = "Không tìm thấy công việc.",
        ["Ticket category not found."] = "Không tìm thấy loại yêu cầu.",
        ["Ticket not found."] = "Không tìm thấy yêu cầu hỗ trợ.",
        ["Unit type not found."] = "Không tìm thấy loại kho.",
        ["Visit not found."] = "Không tìm thấy lượt ra vào.",

        // Accounts and sign-in
        ["An account with this email already exists."] = "Đã có tài khoản dùng email này.",
        ["An account with this email already exists; search for the customer instead."] = "Đã có tài khoản dùng email này; hãy tìm khách hàng đó.",
        ["Invalid email or password."] = "Email hoặc mật khẩu không đúng.",
        ["Please verify your email address before signing in."] = "Vui lòng xác minh email trước khi đăng nhập.",
        ["The current password is incorrect."] = "Mật khẩu hiện tại không đúng.",
        ["The new password must differ from the current one."] = "Mật khẩu mới phải khác mật khẩu hiện tại.",
        ["Password must contain a digit."] = "Mật khẩu phải có ít nhất một chữ số.",
        ["Password must contain a letter."] = "Mật khẩu phải có ít nhất một chữ cái.",
        ["This account is not active."] = "Tài khoản này không hoạt động.",
        ["This reset link is invalid, already used or expired."] = "Liên kết đặt lại mật khẩu không hợp lệ, đã dùng hoặc đã hết hạn.",
        ["This verification link is invalid or has expired."] = "Liên kết xác minh không hợp lệ hoặc đã hết hạn.",
        ["You cannot change your own role."] = "Bạn không thể đổi vai trò của chính mình.",
        ["You cannot deactivate your own account."] = "Bạn không thể vô hiệu tài khoản của chính mình.",
        ["Role must be FacilityStaff, FacilityManager, BusinessOperationsManager or SystemAdministrator."] = "Vai trò phải là Nhân viên cơ sở, Quản lý cơ sở, Quản lý vận hành kinh doanh hoặc Quản trị hệ thống.",
        ["Role must be a staff role."] = "Vai trò phải là vai trò nhân viên.",

        // Facilities, floors and units
        ["The facility does not match the storage unit."] = "Cơ sở không khớp với kho.",
        ["The facility still has reserved or occupied units, or open reservations."] = "Cơ sở vẫn còn kho đã giữ chỗ, đang thuê hoặc đơn đặt đang mở.",
        ["The floor does not belong to this facility."] = "Tầng này không thuộc cơ sở này.",
        ["This facility is not taking bookings yet: no staff work there."] = "Cơ sở này chưa nhận đặt kho: chưa có nhân viên làm việc.",
        ["A unit type that has units or pricing history cannot be deleted."] = "Không thể xóa loại kho đã có kho hoặc lịch sử giá.",
        ["A unit type with this code already exists."] = "Mã loại kho này đã tồn tại.",
        ["Remove a floor with DELETE /api/floors/{id}."] = "Xóa tầng bằng chức năng Xóa tầng.",
        ["The manager must be an active Facility Manager account."] = "Quản lý phải là tài khoản Quản lý cơ sở đang hoạt động.",
        ["You are not assigned to this facility."] = "Bạn không được giao cho cơ sở này.",
        ["You can only assign staff to facilities you manage."] = "Bạn chỉ có thể giao nhân viên cho cơ sở bạn quản lý.",
        ["You do not manage this facility."] = "Bạn không quản lý cơ sở này.",
        ["Only a system administrator can change your own facilities."] = "Chỉ quản trị hệ thống mới đổi được cơ sở của chính bạn.",
        ["This person is not assigned to the facility."] = "Người này không được giao cho cơ sở.",

        // Booking and handover
        ["A reservation can only be marked as a no-show after its move-in date."] = "Chỉ ghi nhận khách không đến sau ngày nhận kho.",
        ["Give either an existing customer or a new customer, not both."] = "Chọn khách hàng cũ hoặc khách hàng mới, không chọn cả hai.",
        ["Move-in date cannot be in the past."] = "Ngày nhận kho không được ở quá khứ.",
        ["No unit of this type is available at this facility."] = "Cơ sở này không còn kho loại này.",
        ["Only a confirmed booking with a unit, not yet handed over, can change its unit."] = "Chỉ đơn đã xác nhận, đã có kho và chưa bàn giao mới đổi được kho.",
        ["The booking already holds this unit."] = "Đơn đặt này đang giữ kho này.",
        ["The contract does not belong to this reservation and unit."] = "Hợp đồng không thuộc đơn đặt và kho này.",
        ["The contract is already signed."] = "Hợp đồng đã được ký.",
        ["The customer has not signed the contract yet."] = "Khách chưa ký hợp đồng.",
        ["The deposit has not been paid yet."] = "Tiền cọc chưa được thanh toán.",
        ["The handover is already cancelled."] = "Bàn giao này đã bị hủy.",
        ["The new unit must be the same size, at the same facility."] = "Kho mới phải cùng cỡ và cùng cơ sở.",
        ["The unit already has an Active or Draft contract."] = "Kho này đã có hợp đồng đang hiệu lực hoặc nháp.",
        ["The unit already has another Active or Draft contract."] = "Kho này đã có hợp đồng khác đang hiệu lực hoặc nháp.",
        ["The unit does not match the chosen facility and size."] = "Kho không khớp với cơ sở và cỡ đã chọn.",
        ["The unit does not match the reservation's facility and unit type."] = "Kho không khớp với cơ sở và loại kho của đơn đặt.",

        // Access codes and visits
        ["Access is not suspended for this case."] = "Hồ sơ này không bị khóa ra vào.",
        ["More than one renter has this code. Check the customer's ID and give them a new code."] = "Có hơn một khách dùng mã này. Hãy kiểm tra giấy tờ và cấp mã mới cho khách.",
        ["No working access code matches. Check the code with the customer."] = "Không có mã ra vào hợp lệ nào khớp. Hãy kiểm tra lại mã với khách.",
        ["The access code is already revoked."] = "Mã ra vào đã bị thu hồi.",
        ["The contract has no active access code."] = "Hợp đồng không có mã ra vào đang dùng.",
        ["The leaving time must be after they came in and not in the future."] = "Giờ rời đi phải sau giờ vào và không ở tương lai.",
        ["This customer has no working access code; they cannot enter."] = "Khách này không có mã ra vào hợp lệ nên không thể vào.",
        ["This customer is already inside; check them out first."] = "Khách này đang ở trong; hãy check-out trước.",
        ["This visit is already checked out."] = "Lượt này đã check-out.",

        // Money
        ["A bank transfer needs its transaction reference."] = "Chuyển khoản cần có mã giao dịch.",
        ["A percentage fee cannot exceed 100."] = "Phí theo phần trăm không được vượt quá 100.",
        ["A refund account is required for a bank transfer."] = "Hoàn tiền qua chuyển khoản cần có tài khoản nhận.",
        ["A reminder for this invoice was already sent today."] = "Hôm nay đã gửi nhắc nhở cho hóa đơn này.",
        ["A written-off invoice cannot be adjusted."] = "Không thể điều chỉnh hóa đơn đã xóa nợ.",
        ["Due date cannot be in the past."] = "Hạn thanh toán không được ở quá khứ.",
        ["Invalid VNPay signature."] = "Chữ ký VNPay không hợp lệ.",
        ["Invalid gateway signature."] = "Chữ ký cổng thanh toán không hợp lệ.",
        ["Payment simulation is not available."] = "Không dùng được thanh toán giả lập.",
        ["Status must be Succeeded or Failed."] = "Trạng thái phải là Thành công hoặc Thất bại.",
        ["The adjustment would make the invoice total negative."] = "Điều chỉnh này khiến tổng hóa đơn bị âm.",
        ["The confirmed amount does not match the payment."] = "Số tiền xác nhận không khớp với thanh toán.",
        ["The customer has already paid more than the adjusted total; refund a payment first."] = "Khách đã trả nhiều hơn tổng sau điều chỉnh; hãy hoàn tiền trước.",
        ["The invoice was updated by another payment. Reload and try again."] = "Hóa đơn vừa được cập nhật bởi một thanh toán khác. Hãy tải lại và thử lại.",
        ["The invoice was updated by another payment. Retry the callback."] = "Hóa đơn vừa được cập nhật bởi một thanh toán khác. Hãy thử lại.",
        ["This deposit refund is already paid."] = "Khoản hoàn cọc này đã được chi.",
        ["This fee has already been billed and cannot be deleted."] = "Phí này đã được tính vào hóa đơn nên không thể xóa.",
        ["This invoice is already closed."] = "Hóa đơn này đã đóng.",

        // Pricing
        ["A later active pricing policy already exists."] = "Đã có chính sách giá áp dụng muộn hơn.",
        ["Publish or delete this facility's current draft first."] = "Hãy áp dụng hoặc xoá bản nháp giá hiện tại của cơ sở này trước.",
        ["Price version not found."] = "Không tìm thấy phiên bản giá.",
        ["This facility has no own price for that size."] = "Cơ sở này chưa có giá riêng cho kích thước đó.",
        ["A pricing policy must have at least one unit price before activation."] = "Chính sách giá phải có ít nhất một giá kho trước khi áp dụng.",
        ["No active pricing policy applies to the move-in date."] = "Không có chính sách giá áp dụng cho ngày nhận kho.",
        ["No price is configured for this unit type in the active policy."] = "Chính sách đang áp dụng chưa có giá cho loại kho này.",
        ["No pricing policy is in force today."] = "Hôm nay không có chính sách giá nào đang áp dụng.",
        ["Only a Draft pricing policy can be edited."] = "Chỉ sửa được chính sách giá ở trạng thái nháp.",
        ["Publish or delete the current draft first."] = "Hãy công bố hoặc xóa bản nháp hiện tại trước.",
        ["The effective date range overlaps an existing pricing policy."] = "Khoảng thời gian hiệu lực trùng với một chính sách giá đã có.",

        // Renewals and returns
        ["The return date cannot be in the past."] = "Ngày trả kho không được ở quá khứ.",
        ["The inspection slot must be in the future."] = "Lịch kiểm tra phải ở tương lai.",
        ["This contract already has a renewal waiting for approval."] = "Hợp đồng này đã có yêu cầu gia hạn đang chờ duyệt.",
        ["This contract already has an open return request."] = "Hợp đồng này đã có yêu cầu trả kho đang mở.",
        ["You have no active contract on this storage unit."] = "Bạn không có hợp đồng đang hiệu lực với kho này.",

        // Support tickets
        ["A Closed ticket cannot change status."] = "Yêu cầu đã đóng không đổi được trạng thái.",
        ["A customer can only close their own ticket."] = "Khách chỉ đóng được yêu cầu của chính mình.",
        ["A reply can only set the status to InProgress or WaitingForCustomer."] = "Phản hồi chỉ đặt được trạng thái Đang xử lý hoặc Chờ khách phản hồi.",
        ["Could not allocate a ticket code, please try again."] = "Không tạo được mã yêu cầu, vui lòng thử lại.",
        ["Only staff can write internal notes or set the ticket status."] = "Chỉ nhân viên mới viết được ghi chú nội bộ hoặc đặt trạng thái yêu cầu.",
        ["Only the customer or a facility manager can close a ticket."] = "Chỉ khách hàng hoặc quản lý cơ sở mới đóng được yêu cầu.",
        ["Provide a storage unit or a facility."] = "Hãy chọn kho hoặc cơ sở.",
        ["Status cannot be set back to Pending."] = "Không thể đặt lại trạng thái Đang chờ.",
        ["The assignee is not assigned to this ticket's facility."] = "Người được giao không thuộc cơ sở của yêu cầu này.",
        ["The assignee must be an active account."] = "Người được giao phải là tài khoản đang hoạt động.",
        ["The assignee must be an active facility staff member or manager."] = "Người được giao phải là nhân viên hoặc quản lý cơ sở đang hoạt động.",
        ["The assignee must be an active staff member of this facility."] = "Người được giao phải là nhân viên đang hoạt động của cơ sở này.",
        ["The storage unit does not match the ticket."] = "Kho không khớp với yêu cầu.",
        ["The ticket is closed and no longer accepts messages."] = "Yêu cầu đã đóng và không nhận thêm tin nhắn.",
        ["The ticket is not about a storage unit, so it cannot lead to a maintenance order."] = "Yêu cầu này không liên quan đến kho nên không tạo được lệnh bảo trì.",

        // Maintenance, shifts and tasks
        ["A photo must be between 1 byte and 5 MB."] = "Ảnh phải từ 1 byte đến 5 MB.",
        ["A shift cannot be moved into the past."] = "Không thể chuyển ca về quá khứ.",
        ["A shift cannot start and end at the same time."] = "Ca làm không thể bắt đầu và kết thúc cùng lúc.",
        ["A shift in the past cannot be created."] = "Không thể tạo ca trong quá khứ.",
        ["A shift that has already ended cannot be changed."] = "Không thể sửa ca đã kết thúc.",
        ["A shift that has started cannot be deleted."] = "Không thể xóa ca đã bắt đầu.",
        ["Give a storageUnitId or a ticketId."] = "Hãy chọn kho hoặc yêu cầu hỗ trợ.",
        ["Only JPEG, PNG or WebP photos are accepted."] = "Chỉ nhận ảnh JPEG, PNG hoặc WebP.",
        ["Only a facility manager can cancel a task."] = "Chỉ quản lý cơ sở mới hủy được công việc.",
        ["Only an Active facility staff member or manager can be given a shift."] = "Chỉ nhân viên hoặc quản lý cơ sở đang hoạt động mới được xếp ca.",
        ["Only the assignee can update the progress of a task."] = "Chỉ người được giao mới cập nhật được tiến độ công việc.",
        ["The task must be assigned to the person on this shift."] = "Công việc phải giao cho người trong ca này.",
        ["This shift has tasks; remove or cancel them first."] = "Ca này có công việc; hãy xóa hoặc hủy trước.",
        ["cost (>= 0) is required to complete an order."] = "Cần nhập chi phí (từ 0 trở lên) để hoàn thành lệnh.",
        ["note is required for a task of type Other."] = "Cần nhập ghi chú cho công việc loại Khác.",
        ["scheduledAt is required to schedule an order."] = "Cần chọn thời gian để lên lịch lệnh.",
        ["scheduledAt must fall inside the shift."] = "Thời gian phải nằm trong ca làm.",
        ["workNote is required to complete an order."] = "Cần nhập ghi chú công việc để hoàn thành lệnh.",

        // Reports
        ["'from' must not be after 'to'."] = "Ngày bắt đầu không được sau ngày kết thúc.",
        ["'to' must be on or after 'from'."] = "Ngày kết thúc không được trước ngày bắt đầu.",
        ["The range cannot exceed three years."] = "Khoảng thời gian không được quá ba năm.",

        // With values
        ["A contract is drafted for a Confirmed reservation with a unit assigned; this one is {0}."] = "Hợp đồng chỉ soạn cho đơn đã xác nhận và đã có kho; đơn này: {0}.",
        ["A deposit is settled only after the return inspection; this return is {0}."] = "Tiền cọc chỉ được quyết toán sau khi kiểm tra trả kho; yêu cầu này: {0}.",
        ["A facility with code {0} already exists."] = "Mã cơ sở {0} đã tồn tại.",
        ["A {0} contract cannot be terminated."] = "Không thể chấm dứt hợp đồng ở trạng thái {0}.",
        ["A {0} order cannot move to {1}."] = "Lệnh {0} không thể chuyển sang {1}.",
        ["A {0} task cannot be cancelled."] = "Không thể hủy công việc ở trạng thái {0}.",
        ["A {0} task cannot move to {1}."] = "Công việc {0} không thể chuyển sang {1}.",
        ["A {0} ticket cannot be assigned."] = "Không thể giao yêu cầu ở trạng thái {0}.",
        ["A {0} ticket cannot move to {1}."] = "Yêu cầu {0} không thể chuyển sang {1}.",
        ["Access codes are issued for an Active contract; this one is {0}."] = "Mã ra vào chỉ cấp cho hợp đồng đang hiệu lực; hợp đồng này: {0}.",
        ["Cannot unassign a facility with open work: {0} upcoming shift(s), {1} open ticket(s), {2} open task(s), {3} open maintenance order(s). Reassign or finish them first."] =
            "Không thể bỏ giao cơ sở khi còn việc: {0} ca sắp tới, {1} yêu cầu, {2} công việc, {3} lệnh bảo trì. Hãy giao lại hoặc hoàn thành trước.",
        ["Customers still hold units on this floor: {0}."] = "Vẫn còn khách thuê kho ở tầng này: {0}.",
        ["Fee code {0} already exists in this policy."] = "Mã phí {0} đã có trong chính sách này.",
        ["Floor {0} already exists in this facility."] = "Tầng {0} đã có trong cơ sở này.",
        ["Only a Draft can be deleted; this version is {0}."] = "Chỉ xóa được bản nháp; phiên bản này: {0}.",
        ["Only a Draft contract can be activated; this one is {0}."] = "Chỉ kích hoạt được hợp đồng nháp; hợp đồng này: {0}.",
        ["Only a Draft contract can be handed over; this one is {0}."] = "Chỉ bàn giao được hợp đồng nháp; hợp đồng này: {0}.",
        ["Only a Draft contract can be signed; this one is {0}."] = "Chỉ ký được hợp đồng nháp; hợp đồng này: {0}.",
        ["Only a Draft policy can be edited; this one is {0}."] = "Chỉ sửa được chính sách nháp; chính sách này: {0}.",
        ["Only a Pending or Confirmed reservation can be closed; this one is {0}."] = "Chỉ đóng được đơn đang chờ hoặc đã xác nhận; đơn này: {0}.",
        ["Only a Requested renewal can be approved; this one is {0}."] = "Chỉ duyệt được yêu cầu gia hạn đang chờ; yêu cầu này: {0}.",
        ["Only a Requested renewal can be rejected; this one is {0}."] = "Chỉ từ chối được yêu cầu gia hạn đang chờ; yêu cầu này: {0}.",
        ["Only a Requested return can be approved; this one is {0}."] = "Chỉ duyệt được yêu cầu trả kho đang chờ; yêu cầu này: {0}.",
        ["Only a Succeeded payment can be refunded; this one is {0}."] = "Chỉ hoàn được thanh toán thành công; thanh toán này: {0}.",
        ["Only a reservation waiting for a unit can be assigned one; this one is {0}."] = "Chỉ xếp kho cho đơn đang chờ xếp; đơn này: {0}.",
        ["Only an Active contract can be handed back; this one is {0}."] = "Chỉ trả kho được với hợp đồng đang hiệu lực; hợp đồng này: {0}.",
        ["Only an Active contract can be renewed; this one is {0}."] = "Chỉ gia hạn được hợp đồng đang hiệu lực; hợp đồng này: {0}.",
        ["Only an Approved return can be inspected; this one is {0}."] = "Chỉ kiểm tra được yêu cầu trả kho đã duyệt; yêu cầu này: {0}.",
        ["Overlaps shift #{0} at {1} ({2} {3}-{4})."] = "Trùng ca #{0} tại {1} ({2} {3}-{4}).",
        ["Payment exceeds the outstanding amount of {0}."] = "Số tiền vượt quá số còn nợ {0}.",
        ["Rental months must be between 1 and {0}."] = "Số tháng thuê phải từ 1 đến {0}.",
        ["Role '{0}' does not exist."] = "Vai trò '{0}' không tồn tại.",
        ["The contract is {0}; only the handover of an Active contract can be cancelled."] = "Hợp đồng đang ở trạng thái {0}; chỉ hủy được bàn giao của hợp đồng đang hiệu lực.",
        ["The payment is already {0}."] = "Thanh toán đã ở trạng thái {0}.",
        ["The reservation must be Confirmed with a unit assigned; it is {0}."] = "Đơn đặt phải được xác nhận và có kho; đơn này: {0}.",
        ["The return inspection is {0}; resolve it before settling."] = "Kết quả kiểm tra trả kho: {0}; hãy xử lý trước khi quyết toán.",
        ["The ticket is already {0}."] = "Yêu cầu đã ở trạng thái {0}.",
        ["The unit must be emptied within {0} days, by {1}."] = "Phải dọn kho trong {0} ngày, trước {1}.",
        ["This case is already {0}."] = "Hồ sơ này đã ở trạng thái {0}.",
        ["This policy starts on {0}; activate it on or after that date."] = "Chính sách này bắt đầu từ {0}; hãy áp dụng từ ngày đó trở đi.",
        ["Unit code {0} is already used in this facility."] = "Mã kho {0} đã được dùng trong cơ sở này.",
        ["Unit {0} already has an Active or Draft contract."] = "Kho {0} đã có hợp đồng đang hiệu lực hoặc nháp.",
        ["Unit {0} is retired; restore it before ordering work on it."] = "Kho {0} đã ngừng sử dụng; hãy khôi phục trước khi tạo lệnh.",
        ["Unit {0} is {1}, expected Reserved."] = "Kho {0} đang ở trạng thái {1}, cần là đã giữ chỗ.",
        ["Unit {0} is {1}, not Available."] = "Kho {0} đang ở trạng thái {1}, không còn trống.",
        ["Unit {0} is {1}: a customer holds it. Order work on it from their open support ticket."] = "Kho {0} đang ở trạng thái {1}: có khách đang giữ. Hãy tạo lệnh từ yêu cầu hỗ trợ đang mở của khách.",
        ["Unit {0} is {1}; its type and floor can only change while it is Available."] = "Kho {0} đang ở trạng thái {1}; chỉ đổi được loại và tầng khi kho còn trống.",
        ["Unit {0} is {1}; only an {2} unit can become {3}."] = "Kho {0} đang ở trạng thái {1}; chỉ kho {2} mới chuyển được sang {3}.",
    };

    private static readonly (Regex Pattern, string Text)[] Templates = Vietnamese
        .Where(entry => Hole.IsMatch(entry.Key))
        .Select(entry => (
            new Regex("^" + Regex.Replace(Regex.Escape(entry.Key), @"\\\{\d}", "(.+?)") + "$", RegexOptions.Singleline),
            entry.Value))
        .ToArray();
}
