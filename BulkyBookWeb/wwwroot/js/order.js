let dataTable;
$(document).ready(function () {
    var url = window.location.search;
    if (url.includes("inprocess")) {
        LoadDataTable("inprocess");
    }
    else {
        if (url.includes("completed")) {
            LoadDataTable("completed");
        }
        else {
            if (url.includes("approved")) {
                LoadDataTable("approved");
            }
            else {
                if (url.includes("pending")) {
                    LoadDataTable("pending");
                }
                else {
                    if (url.includes("apprived")) {
                        LoadDataTable("apprived");
                    }
                    else {
                        LoadDataTable("all")
                    }
                }
            }
        }
    }
});

function LoadDataTable(status) {
        dataTable = $('#myTable').DataTable(
            {
                ajax: {
                    url: '/admin/order/getall?status=' + status,
                    dataSrc: 'data'
                },
                columns: [
                    { data: 'id', "width": "5%" },
                    { data: 'name', "width": "25%" },
                    { data: 'phoneNumber', "width": "20%" },
                    { data: 'applicationUser.email', "width": "20%" },
                    { data: 'orderStatus', "width": "10%" },
                    { data: 'orderTotal', "width": "10%" },
                    {
                        data: 'id',
                        "render": function (data) {
                            return `<div class"w-75 btn group row d-flex" role="group">
                            <a href="/admin/order/details?orderId=${data}" class="btn btn-primary mx-2"> <i class="bi bi-pencil-square"></i></a>
                        </div>`
                        },
                        "width": "10"
                    }
                ]
            }
        )
    }

